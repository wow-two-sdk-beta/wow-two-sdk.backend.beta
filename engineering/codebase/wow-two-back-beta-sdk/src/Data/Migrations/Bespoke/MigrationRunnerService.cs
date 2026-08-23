using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using Dapper;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results; // IDbConnectionFactory — the SDK connection seam (returns BCL DbConnection).

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Provides the migration apply, status, rollback, and repair operations behind every host.</summary>
/// <remarks>
/// Apply flow:
///   1. Scan the source into ordered, checksummed descriptors
///   2. Open a connection and acquire the session advisory lock
///   3. Ensure the history table, read applied rows, verify no drift
///   4. Apply each pending migration (per-file transaction unless no-transaction)
///   5. Release the lock
/// </remarks>
public sealed partial class MigrationRunnerService(
    IMigrationSource source,
    IMigrationHistoryRepository history,
    IDbConnectionFactory connections,
    MigrationOptions options,
    ILogger<MigrationRunnerService> logger) : IMigrationRunnerService
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> ApplyPendingAsync(string appliedBy, CancellationToken ct = default)
    {
        if (Scan().IsFailure(out var malformed, out var migrations))
        {
            return Result<IReadOnlyList<string>>.Fail(malformed);
        }

        await using var conn = await connections.CreateOpenAsync(ct);

        // Acquire the advisory lock so only one host migrates at a time.
        await history.AcquireLockAsync(conn, ct);
        try
        {
            await history.EnsureTableAsync(conn, ct);
            var applied = await history.GetAppliedAsync(conn, ct);

            if (VerifyNoDrift(migrations, applied) is { } drift)
            {
                return Result<IReadOnlyList<string>>.Fail(drift);
            }

            if (VerifyNoOrphans(migrations, applied) is { } orphans)
            {
                return Result<IReadOnlyList<string>>.Fail(orphans);
            }

            var appliedOrdinals = applied.Select(a => a.Ordinal).ToHashSet();
            var pending = migrations.Where(m => !appliedOrdinals.Contains(m.Ordinal)).ToList();
            if (pending.Count == 0)
            {
                LogUpToDate(applied.Count);
                return Result<IReadOnlyList<string>>.Ok([]);
            }

            var done = new List<string>();
            foreach (var migration in pending)
            {
                var elapsedMs = await ApplyOneAsync(conn, migration, appliedBy, ct);
                done.Add(migration.Label);
                LogApplied(migration.Label, elapsedMs);
            }

            return Result<IReadOnlyList<string>>.Ok(done);
        }
        finally
        {
            await history.ReleaseLockAsync(conn, ct);
        }
    }

    /// <summary>Applies one migration and records it, returning the elapsed milliseconds.</summary>
    /// <param name="conn">The open connection holding the advisory lock.</param>
    /// <param name="migration">The migration to apply.</param>
    /// <param name="appliedBy">The host stamp recorded on the row.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    private async Task<long> ApplyOneAsync(DbConnection conn, MigrationDescriptor migration, string appliedBy, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        // No-transaction migrations run bare (e.g. CREATE INDEX CONCURRENTLY), then record in a separate statement.
        // A crash between the two leaves the change applied but unrecorded, so the next run RE-EXECUTES the script —
        // the author MUST make the Apply SQL idempotent (IF NOT EXISTS / guarded DO blocks).
        if (migration.NoTransaction)
        {
            await conn.ExecuteAsync(new CommandDefinition(migration.ApplySql, cancellationToken: ct));
            stopwatch.Stop();
            await history.RecordAsync(conn, null, BuildEntry(migration, appliedBy, stopwatch.ElapsedMilliseconds), ct);
            return stopwatch.ElapsedMilliseconds;
        }

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(migration.ApplySql, transaction: tx, cancellationToken: ct));
            stopwatch.Stop();
            await history.RecordAsync(conn, tx, BuildEntry(migration, appliedBy, stopwatch.ElapsedMilliseconds), ct);
            await tx.CommitAsync(ct);
            return stopwatch.ElapsedMilliseconds;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<MigrationStatus>> GetStatusAsync(CancellationToken ct = default)
    {
        if (Scan().IsFailure(out var malformed, out var migrations))
        {
            return Result<MigrationStatus>.Fail(malformed);
        }

        var byOrdinal = migrations.ToDictionary(m => m.Ordinal);

        await using var conn = await connections.CreateOpenAsync(ct);
        await history.EnsureTableAsync(conn, ct);
        var applied = await history.GetAppliedAsync(conn, ct);
        var appliedOrdinals = applied.Select(a => a.Ordinal).ToHashSet();

        // Diff source against applied to classify drift, orphans, and pending.
        var drifted = applied
            .Where(a => byOrdinal.TryGetValue(a.Ordinal, out var m) && m.Checksum != a.Checksum.Trim())
            .Select(a => byOrdinal[a.Ordinal])
            .ToList();
        var orphaned = applied.Where(a => !byOrdinal.ContainsKey(a.Ordinal)).Select(a => a.Ordinal).ToList();
        var pending = migrations.Where(m => !appliedOrdinals.Contains(m.Ordinal)).ToList();

        return Result<MigrationStatus>.Ok(
            new MigrationStatus { Applied = applied, Pending = pending, Drifted = drifted, Orphaned = orphaned });
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> RollbackAsync(int? targetOrdinal = null, CancellationToken ct = default)
    {
        // Configuration, not a domain failure: a host that disabled rollback and called it anyway is misconfigured, and the
        // framework's own seams only catch what is thrown. Stays an exception under N82.
        if (!options.AllowRollback)
            throw new InvalidOperationException(
                "Rollback is disabled (MigrationOptions.AllowRollback = false). Enable it to permit this guarded recovery op (allowed in any environment, prod included).");

        if (Scan().IsFailure(out var malformed, out var migrations))
        {
            return Result<IReadOnlyList<string>>.Fail(malformed);
        }

        var byOrdinal = migrations.ToDictionary(m => m.Ordinal);

        await using var conn = await connections.CreateOpenAsync(ct);
        await history.AcquireLockAsync(conn, ct);
        try
        {
            await history.EnsureTableAsync(conn, ct);
            var applied = (await history.GetAppliedAsync(conn, ct)).OrderByDescending(a => a.Ordinal).ToList();

            // Roll back everything strictly above the target, or just the single latest when no target is given.
            var toRollback = targetOrdinal is { } target
                ? applied.Where(a => a.Ordinal > target).ToList()
                : applied.Take(1).ToList();

            var done = new List<string>();
            foreach (var row in toRollback)
            {
                if (!byOrdinal.TryGetValue(row.Ordinal, out var migration))
                {
                    return Result<IReadOnlyList<string>>.Fail(AppErrorFactory.NotFound(
                        $"No migration source for {row.Ordinal:D3}-{row.Name}; cannot roll back."));
                }

                await using var tx = await conn.BeginTransactionAsync(ct);
                try
                {
                    await conn.ExecuteAsync(new CommandDefinition(migration.RollbackSql, transaction: tx, cancellationToken: ct));
                    await history.RemoveAsync(conn, tx, row.Ordinal, ct);
                    await tx.CommitAsync(ct);
                    done.Add(migration.Label);
                    LogRolledBack(migration.Label);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }

            return Result<IReadOnlyList<string>>.Ok(done);
        }
        finally
        {
            await history.ReleaseLockAsync(conn, ct);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> RepairAsync(CancellationToken ct = default)
    {
        // Configuration, same as RollbackAsync — stays an exception under N82.
        if (!options.AllowRollback)
            throw new InvalidOperationException(
                "Repair is disabled (MigrationOptions.AllowRollback = false) — it rewrites recorded checksums; enable it to permit this guarded recovery op.");

        if (Scan().IsFailure(out var malformed, out var migrations))
        {
            return Result<IReadOnlyList<string>>.Fail(malformed);
        }

        var byOrdinal = migrations.ToDictionary(m => m.Ordinal);

        await using var conn = await connections.CreateOpenAsync(ct);

        // Repair mutates migration_history, so hold the same advisory lock the apply/rollback loops use.
        await history.AcquireLockAsync(conn, ct);
        try
        {
            await history.EnsureTableAsync(conn, ct);
            var applied = await history.GetAppliedAsync(conn, ct);

            var repaired = new List<string>();
            foreach (var row in applied)
            {
                // Re-record only the rows whose source checksum has drifted.
                if (byOrdinal.TryGetValue(row.Ordinal, out var migration) && migration.Checksum != row.Checksum.Trim())
                {
                    await history.UpdateChecksumAsync(conn, row.Ordinal, migration.Checksum, ct);
                    repaired.Add(migration.Label);
                }
            }

            return Result<IReadOnlyList<string>>.Ok(repaired);
        }
        finally
        {
            await history.ReleaseLockAsync(conn, ct);
        }
    }

    // Scanning is a private step, not a seam: source pluggability lives in IMigrationSource, and this parse is the only
    // reading of a RawMigration there will ever be. Splitting it behind an interface bought one implementation, one
    // consumer and no test fake.
    /// <summary>Reads the source, parses <c>NNN-name</c>, computes checksums, and returns migrations ordered by ordinal.</summary>
    private Result<List<MigrationDescriptor>> Scan()
    {
        var descriptors = new List<MigrationDescriptor>();

        foreach (var raw in source.Read())
        {
            // Parse the NNN-name prefix; reject anything that does not match.
            var match = MigrationConventions.FolderPattern().Match(raw.Name);
            if (!match.Success)
            {
                return Result<List<MigrationDescriptor>>.Fail(AppErrorFactory.Validation(
                    $"Migration folder '{raw.Name}' must match NNN-name (e.g. 001-baseline)."));
            }

            descriptors.Add(new MigrationDescriptor
            {
                Ordinal = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                Name = match.Groups[2].Value,
                ApplySql = raw.ApplySql,
                RollbackSql = raw.RollbackSql,
                Checksum = raw.ApplySql.ToMigrationChecksum(),
                NoTransaction = HasNoTransactionDirective(raw.ApplySql),
            });
        }

        // Reject two migrations claiming the same ordinal.
        var duplicate = descriptors.GroupBy(d => d.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return Result<List<MigrationDescriptor>>.Fail(AppErrorFactory.Validation(
                $"Duplicate migration ordinal {duplicate.Key:D3}: {string.Join(", ", duplicate.Select(d => d.Name))}."));
        }

        return Result<List<MigrationDescriptor>>.Ok(descriptors.OrderBy(d => d.Ordinal).ToList());
    }

    /// <summary>Gets whether the leading comment header contains a no-transaction directive.</summary>
    /// <param name="applySql">The Apply script to inspect.</param>
    private static bool HasNoTransactionDirective(string applySql)
    {
        using var reader = new StringReader(applySql);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;
            if (!trimmed.StartsWith("--", StringComparison.Ordinal))
                break; // The first non-comment line ends the header.
            if (trimmed.Replace(" ", "").Equals(MigrationConventions.NoTransactionDirectiveCompact, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Verifies no applied migration's source checksum has drifted.</summary>
    /// <param name="migrations">The scanned source migrations.</param>
    /// <param name="applied">The applied history rows to compare against.</param>
    /// <returns>The drift error, or <see langword="null"/> when every applied checksum still matches.</returns>
    private static AppError? VerifyNoDrift(IReadOnlyList<MigrationDescriptor> migrations, IReadOnlyList<MigrationHistoryEntry> applied)
    {
        var byOrdinal = migrations.ToDictionary(m => m.Ordinal);
        var drifted = applied
            .Where(a => byOrdinal.TryGetValue(a.Ordinal, out var m) && m.Checksum != a.Checksum.Trim())
            .Select(a => byOrdinal[a.Ordinal].Label)
            .ToList();

        if (drifted.Count == 0)
        {
            return null;
        }

        return AppError.Of(
            AppErrorType.DataIntegrity,
            $"Applied migrations no longer match their source: {string.Join(", ", drifted)}.",
            new Dictionary<string, object?> { ["drifted"] = drifted });
    }

    /// <summary>Fails closed when the history holds applied migrations absent from the source — unless orphans are explicitly allowed.</summary>
    /// <param name="migrations">The scanned source migrations.</param>
    /// <param name="applied">The applied history rows to compare against.</param>
    /// <returns>The orphan error, or <see langword="null"/> when there are none or they are allowed.</returns>
    private AppError? VerifyNoOrphans(IReadOnlyList<MigrationDescriptor> migrations, IReadOnlyList<MigrationHistoryEntry> applied)
    {
        var sourceOrdinals = migrations.Select(m => m.Ordinal).ToHashSet();
        var orphaned = applied.Where(a => !sourceOrdinals.Contains(a.Ordinal)).Select(a => a.Ordinal).ToList();
        if (orphaned.Count == 0)
        {
            return null;
        }

        if (options.AllowOrphanedHistory)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                LogOrphanedHistory(string.Join(", ", orphaned.Select(o => o.ToString("D3", CultureInfo.InvariantCulture))));
            return null;
        }

        return AppError.Of(
            AppErrorType.DataIntegrity,
            $"History holds applied migrations absent from the source: {string.Join(", ", orphaned.Select(o => o.ToString("D3", CultureInfo.InvariantCulture)))}.",
            new Dictionary<string, object?> { ["orphaned"] = orphaned });
    }

    /// <summary>Builds the history entry recorded for an applied migration.</summary>
    /// <param name="migration">The applied migration.</param>
    /// <param name="appliedBy">The host stamp recorded on the row.</param>
    /// <param name="elapsedMs">The apply duration in milliseconds.</param>
    private MigrationHistoryEntry BuildEntry(MigrationDescriptor migration, string appliedBy, long elapsedMs) => new()
    {
        Ordinal = migration.Ordinal,
        Version = options.Version,
        Name = migration.Name,
        Checksum = migration.Checksum,
        AppliedAt = DateTimeOffset.UtcNow,
        AppliedBy = appliedBy,
        ExecutionMs = (int)elapsedMs,
    };

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Migrations up to date ({Count} applied).")]
    private partial void LogUpToDate(int count);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Applied {Migration} in {Elapsed}ms.")]
    private partial void LogApplied(string migration, long elapsed);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Rolled back {Migration}.")]
    private partial void LogRolledBack(string migration);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Warning, Message = "Applying over orphaned history (ordinals {Orphaned}) — running binary may be older than the database.")]
    private partial void LogOrphanedHistory(string orphaned);
}
