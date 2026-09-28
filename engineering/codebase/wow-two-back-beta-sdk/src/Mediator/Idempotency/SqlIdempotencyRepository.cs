using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>
/// Accesses idempotency records in a SQL table (PostgreSQL or SQLite) through the autonomous
/// <see cref="IDbConnectionFactory"/>, so every host sees an acquisition at once and a stored response survives restarts.
/// Keys are stored by SHA-256 digest; an in-progress key carries a lease another host may take over once it lapses.
/// </summary>
/// <param name="connections">Opens autonomous connections, outside any request transaction.</param>
/// <param name="options">Table, lease and serializer settings.</param>
/// <param name="timeProvider">The clock leases and expiry use.</param>
public sealed class SqlIdempotencyRepository(IDbConnectionFactory connections, SqlIdempotencyOptions options, TimeProvider timeProvider) : IIdempotencyRepository
{
    private const int MaxAttempts = 3;

    /// <summary>The idempotent DDL for the table; runs on PostgreSQL and SQLite.</summary>
    public string CreateTableSql =>
        $"""
        CREATE TABLE IF NOT EXISTS {options.TableName} (
            key_hash TEXT PRIMARY KEY,
            ownership TEXT NOT NULL,
            response_type TEXT NOT NULL,
            response_json TEXT NULL,
            completed INTEGER NOT NULL DEFAULT 0,
            expires_at BIGINT NOT NULL
        )
        """;

    /// <summary>Create the table when it is missing; production schemas normally come from a migration running <see cref="CreateTableSql"/>.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureTableAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.CreateOpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(CreateTableSql, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<(bool Acquired, object? CachedResponse, Guid Ownership)> TryAcquireAsync(string key, Type responseType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(responseType);

        var keyHash = Digest(key);
        var contract = responseType.FullName ?? responseType.Name;
        await using var connection = await connections.CreateOpenAsync(cancellationToken);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var ownership = Guid.NewGuid();
            var now = timeProvider.GetUtcNow();
            var lease = (now + options.PendingLease).ToUnixTimeMilliseconds();
            var inserted = await connection.ExecuteAsync(new CommandDefinition(
                $"""
                INSERT INTO {options.TableName} (key_hash, ownership, response_type, completed, expires_at)
                VALUES (@KeyHash, @Ownership, @Contract, 0, @Lease)
                ON CONFLICT (key_hash) DO NOTHING
                """,
                new { KeyHash = keyHash, Ownership = ownership.ToString("N"), Contract = contract, Lease = lease },
                cancellationToken: cancellationToken));
            if (inserted == 1)
                return (true, null, ownership);

            var row = await connection.QuerySingleOrDefaultAsync<RecordRow>(new CommandDefinition(
                $"SELECT ownership AS Ownership, response_type AS Contract, response_json AS ResponseJson, completed AS Completed, expires_at AS ExpiresAt FROM {options.TableName} WHERE key_hash = @KeyHash",
                new { KeyHash = keyHash },
                cancellationToken: cancellationToken));
            if (row is null)
                continue;

            var live = row.ExpiresAt > now.ToUnixTimeMilliseconds();
            if (live && row.Completed != 0)
            {
                if (!string.Equals(row.Contract, contract, StringComparison.Ordinal))
                    throw AppErrorFactory.Conflict("The idempotency key belongs to another response contract.").ToException();

                return (false, row.ResponseJson is null ? null : JsonSerializer.Deserialize(row.ResponseJson, responseType, options.SerializerOptions), Guid.Empty);
            }

            if (live)
                throw AppErrorFactory.Conflict("An operation with this idempotency key is still in progress.").ToException();

            var takenOver = await connection.ExecuteAsync(new CommandDefinition(
                $"""
                UPDATE {options.TableName}
                SET ownership = @Ownership, response_type = @Contract, response_json = NULL, completed = 0, expires_at = @Lease
                WHERE key_hash = @KeyHash AND ownership = @Previous
                """,
                new { KeyHash = keyHash, Ownership = ownership.ToString("N"), Contract = contract, Lease = lease, Previous = row.Ownership },
                cancellationToken: cancellationToken));
            if (takenOver == 1)
                return (true, null, ownership);
        }

        throw AppErrorFactory.Conflict("An operation with this idempotency key is still in progress.").ToException();
    }

    /// <inheritdoc />
    public async Task StoreAsync(string key, Guid ownership, object? response, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

        var json = response is null ? null : JsonSerializer.Serialize(response, response.GetType(), options.SerializerOptions);
        await using var connection = await connections.CreateOpenAsync(cancellationToken);
        var stored = await connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE {options.TableName}
            SET response_json = @Json, completed = 1, expires_at = @Expires
            WHERE key_hash = @KeyHash AND ownership = @Ownership AND completed = 0
            """,
            new { KeyHash = Digest(key), Ownership = ownership.ToString("N"), Json = json, Expires = (timeProvider.GetUtcNow() + ttl).ToUnixTimeMilliseconds() },
            cancellationToken: cancellationToken));
        if (stored != 1)
            throw new InvalidOperationException("Acquire the idempotency key before storing its response; the lease may have passed to another host.");
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(string key, Guid ownership, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await using var connection = await connections.CreateOpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {options.TableName} WHERE key_hash = @KeyHash AND ownership = @Ownership AND completed = 0",
            new { KeyHash = Digest(key), Ownership = ownership.ToString("N") },
            cancellationToken: cancellationToken));
    }

    /// <summary>Delete expired responses and lapsed leases; schedule it from a recurring job.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many records were deleted.</returns>
    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.CreateOpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {options.TableName} WHERE expires_at <= @Now",
            new { Now = timeProvider.GetUtcNow().ToUnixTimeMilliseconds() },
            cancellationToken: cancellationToken));
    }

    private static string Digest(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private sealed record RecordRow
    {
        public required string Ownership { get; init; }

        public required string Contract { get; init; }

        public string? ResponseJson { get; init; }

        public long Completed { get; init; }

        public long ExpiresAt { get; init; }
    }
}
