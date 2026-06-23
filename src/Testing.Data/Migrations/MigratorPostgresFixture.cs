using Npgsql;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Backend.Beta.Testing.Containers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

/// <summary>Owns one Postgres container for a migrator suite; <see cref="ResetAsync"/> drops and recreates the <c>public</c> schema so each test applies from scratch.</summary>
/// <remarks>The deliberate opposite of the base <c>PostgresFixture</c>, whose Respawn reset preserves <c>migration_history</c>; migrator tests need the full drop for apply-from-scratch, drift, and rollback. Implements xUnit <see cref="IAsyncLifetime"/> so it serves directly as an <c>ICollectionFixture</c>.</remarks>
public sealed class MigratorPostgresFixture : ContainerFixtureBase<PostgreSqlContainer>, IAsyncLifetime
{
    // A single public constructor — xUnit collection fixtures require exactly one, and the image is pinned so a suite never floats across engine versions.
    /// <summary>Creates a fixture over a pinned Postgres image.</summary>
    public MigratorPostgresFixture() : base(new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build())
    {
    }

    /// <inheritdoc />
    public override string Name => "postgres-migrator";

    /// <summary>The connection string of the started container.</summary>
    public string ConnectionString => Container.GetConnectionString();

    /// <summary>Drops and recreates the <c>public</c> schema, wiping every object including <c>migration_history</c>.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public override async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "drop schema public cascade; create schema public;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts the container for xUnit, so the fixture serves directly as an <c>ICollectionFixture</c>.</summary>
    Task IAsyncLifetime.InitializeAsync() => StartAsync().AsTask();

    /// <summary>Disposes the container for xUnit's <see cref="IAsyncLifetime"/> contract.</summary>
    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}
