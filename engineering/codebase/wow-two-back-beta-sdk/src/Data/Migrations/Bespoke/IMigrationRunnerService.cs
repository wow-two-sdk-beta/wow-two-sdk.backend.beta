using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Defines running pending migrations.</summary>
public interface IMigrationRunnerService
{
    /// <summary>Applies all pending migrations under the advisory lock, returning the labels applied (empty when up to date).</summary>
    /// <param name="appliedBy">The host stamp recorded on each applied row, such as <c>startup</c>, <c>endpoint</c>, or <c>cli</c>.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>The labels applied, or a <c>DataIntegrity</c> failure on drift or disallowed orphans, or a <c>Validation</c> failure on a malformed source.</returns>
    Task<Result<IReadOnlyList<string>>> ApplyPendingAsync(string appliedBy, CancellationToken ct = default);

    /// <summary>Computes the current state: applied, pending, drifted, and orphaned migrations.</summary>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<Result<MigrationStatus>> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Rolls back the most recent migration, or every migration above the given ordinal (dev/test only).</summary>
    /// <param name="targetOrdinal">The ordinal to roll back to, or null to roll back only the latest.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <returns>The labels rolled back, or a <c>NotFound</c> failure when an applied ordinal has no source migration.</returns>
    /// <exception cref="InvalidOperationException">Rollback is disabled — configuration, so it throws rather than returning a failure.</exception>
    Task<Result<IReadOnlyList<string>>> RollbackAsync(int? targetOrdinal = null, CancellationToken ct = default);

    /// <summary>Re-records the stored checksums of drifted migrations to match the source under the advisory lock (dev/test only).</summary>
    /// <param name="ct">Token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Repair is disabled (<see cref="MigrationOptions.AllowRollback"/> is false) — configuration, so it throws.</exception>
    Task<Result<IReadOnlyList<string>>> RepairAsync(CancellationToken ct = default);
}
