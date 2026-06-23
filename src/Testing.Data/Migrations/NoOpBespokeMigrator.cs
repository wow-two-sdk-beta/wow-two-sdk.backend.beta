using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

/// <summary>Service-collection helper disabling the bespoke migrator's startup hook for tests whose schema comes from elsewhere (e.g. EF <c>EnsureCreated</c>).</summary>
public static class NoOpBespokeMigratorExtensions
{
    /// <summary>Replaces the bespoke migrator dialect and runner with no-ops so the startup migrate hook touches no database.</summary>
    /// <param name="services">The service collection to neuter.</param>
    public static IServiceCollection DisableBespokeMigrator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IMigrationDialect>();
        services.AddSingleton<IMigrationDialect, NoOpMigrationDialect>();
        services.RemoveAll<IMigrationRunnerService>();
        services.AddSingleton<IMigrationRunnerService, NoOpMigrationRunner>();
        return services;
    }
}

/// <summary>No-op <see cref="IMigrationDialect"/> keeping a startup migrate hook off the real database.</summary>
internal sealed class NoOpMigrationDialect : IMigrationDialect
{
    /// <inheritdoc />
    public Task<bool> EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task AcquireLockAsync(DbConnection connection, long lockId, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task ReleaseLockAsync(DbConnection connection, long lockId, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task EnsureHistoryTableAsync(DbConnection connection, string schemaName, string tableName, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public string QualifyHistoryTable(string schemaName, string tableName) => $"\"{tableName}\"";
}

/// <summary>No-op <see cref="IMigrationRunnerService"/> for tests whose schema is created outside the bespoke migrator.</summary>
internal sealed class NoOpMigrationRunner : IMigrationRunnerService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ApplyPendingAsync(string appliedBy, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);

    /// <inheritdoc />
    public Task<MigrationStatus> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(new MigrationStatus { Applied = [], Pending = [], Drifted = [], Orphaned = [] });

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> RollbackAsync(int? targetOrdinal = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> RepairAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);
}
