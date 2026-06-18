using System.Data.Common;
using Dapper;
using Npgsql;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Provides the PostgreSQL dialect: maintenance-DB create, advisory locking, and history-table DDL.</summary>
/// <remarks>Use as the <see cref="IMigrationDialect"/> when <see cref="MigrationOptions.Provider"/> is <see cref="DatabaseProvider.Postgres"/>.</remarks>
public sealed class PostgresMigrationDialect : IMigrationDialect
{
    private const string MaintenanceDatabase = "postgres";

    /// <inheritdoc />
    /// <remarks>Connects to the <c>postgres</c> maintenance database, since the pinned data source cannot create its own target database.</remarks>
    public async Task<bool> EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
            return false;

        // Switch to the maintenance database — the target database may not exist yet.
        builder.Database = MaintenanceDatabase;

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);

        await using var check = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
        check.Parameters.AddWithValue("name", databaseName);
        if (await check.ExecuteScalarAsync(ct) is not null)
            return false;

        // CREATE DATABASE cannot be parameterized; the name comes from config, not user input.
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await create.ExecuteNonQueryAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public Task AcquireLockAsync(DbConnection connection, long lockId, CancellationToken ct = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_lock(@id)", new { id = lockId }, cancellationToken: ct));

    /// <inheritdoc />
    public Task ReleaseLockAsync(DbConnection connection, long lockId, CancellationToken ct = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_unlock(@id)", new { id = lockId }, cancellationToken: ct));

    /// <inheritdoc />
    public Task EnsureHistoryTableAsync(DbConnection connection, string schemaName, string tableName, CancellationToken ct = default)
    {
        var sql = $"""
            CREATE TABLE IF NOT EXISTS {QualifyHistoryTable(schemaName, tableName)} (
                ordinal       integer      NOT NULL,
                version       varchar(20)  NOT NULL,
                name          varchar(120) NOT NULL,
                checksum      char(64)     NOT NULL,
                applied_at    timestamptz  NOT NULL,
                applied_by    varchar(60)  NOT NULL,
                execution_ms  integer      NOT NULL,
                CONSTRAINT pk_{tableName} PRIMARY KEY (ordinal)
            );
            """;
        return connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }

    /// <inheritdoc />
    public string QualifyHistoryTable(string schemaName, string tableName) => $"\"{schemaName}\".\"{tableName}\"";
}
