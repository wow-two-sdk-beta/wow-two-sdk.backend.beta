namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Defines the contract for running pending migrations.</summary>
public interface IMigrationRunnerService
{
    /// <summary>Applies all pending migrations under the advisory lock, returning the labels applied (empty when up to date).</summary>
    /// <remarks>No-transaction migrations are recorded separately from their apply, so their Apply SQL must be idempotent (a crash mid-apply re-runs it).</remarks>
    /// <param name="appliedBy">The host stamp recorded on each applied row, such as <c>startup</c>, <c>endpoint</c>, or <c>cli</c>.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <exception cref="MigrationDriftException">An applied migration's checksum no longer matches its source.</exception>
    /// <exception cref="MigrationOrphanException">The history has applied migrations missing from the source and orphans are not allowed.</exception>
    Task<IReadOnlyList<string>> ApplyPendingAsync(string appliedBy, CancellationToken ct = default);

    /// <summary>Computes the current state: applied, pending, drifted, and orphaned migrations.</summary>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<MigrationStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Rolls back the most recent migration, or every migration above the given ordinal (dev/test only).</summary>
    /// <param name="targetOrdinal">The ordinal to roll back to, or null to roll back only the latest.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Rollback is disabled, or an applied ordinal has no matching source migration.</exception>
    Task<IReadOnlyList<string>> RollbackAsync(int? targetOrdinal = null, CancellationToken ct = default);

    /// <summary>Re-records the stored checksums of drifted migrations to match the source under the advisory lock (dev/test only).</summary>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Repair is disabled (<see cref="MigrationOptions.AllowRollback"/> is false).</exception>
    Task<IReadOnlyList<string>> RepairAsync(CancellationToken ct = default);
}
