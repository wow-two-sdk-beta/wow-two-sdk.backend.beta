using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

/// <summary>No-op <see cref="IMigrationDialect"/> keeping a startup migrate hook off the real database.</summary>
internal sealed class NoOpMigrationDialect : IMigrationDialect
{
    /// <inheritdoc />
    public MigrationCoordinationMode CoordinationMode => MigrationCoordinationMode.SingleApplicantRequired;

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
