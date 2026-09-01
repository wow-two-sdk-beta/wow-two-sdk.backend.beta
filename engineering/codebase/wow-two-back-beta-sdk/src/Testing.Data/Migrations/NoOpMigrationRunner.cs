using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

/// <summary>No-op <see cref="IMigrationRunnerService"/> for tests whose schema is created outside the bespoke migrator.</summary>
internal sealed class NoOpMigrationRunner : IMigrationRunnerService
{
    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> ApplyPendingAsync(string appliedBy, CancellationToken ct = default) =>
        Task.FromResult(Result<IReadOnlyList<string>>.Ok([]));

    /// <inheritdoc />
    public Task<Result<MigrationStatus>> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<MigrationStatus>.Ok(
            new MigrationStatus { Applied = [], Pending = [], Drifted = [], Orphaned = [] }));

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> RollbackAsync(int? targetOrdinal = null, CancellationToken ct = default) =>
        Task.FromResult(Result<IReadOnlyList<string>>.Ok([]));

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> RepairAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<IReadOnlyList<string>>.Ok([]));
}
