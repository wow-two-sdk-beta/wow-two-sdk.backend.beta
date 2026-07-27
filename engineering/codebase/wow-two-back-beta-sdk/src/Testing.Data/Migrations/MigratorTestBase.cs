using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

/// <summary>Convenience base for Postgres migrator tests — resets the shared fixture's schema per test and owns a fresh filesystem <see cref="MigrationsWorkspace"/>.</summary>
/// <param name="fixture">The shared drop-schema Postgres fixture supplied by the test collection.</param>
public abstract class MigratorTestBase(MigratorPostgresFixture fixture) : IAsyncLifetime
{
    /// <summary>The shared Postgres migrator fixture.</summary>
    protected MigratorPostgresFixture Fixture { get; } = fixture;

    /// <summary>This test's throwaway on-disk migrations root.</summary>
    protected MigrationsWorkspace Workspace { get; } = new();

    /// <summary>Builds a migrator over this test's workspace against the shared container DB.</summary>
    /// <param name="configure">An optional hook to override <see cref="MigrationOptions"/>.</param>
    protected MigratorHarness CreateMigrator(Action<MigrationOptions>? configure = null) =>
        MigratorHarness.CreatePostgres(Fixture.ConnectionString, Workspace.Root, configure);

    /// <summary>Drops and recreates the schema so the test starts clean.</summary>
    public async Task InitializeAsync() => await Fixture.ResetAsync().ConfigureAwait(false);

    /// <summary>Disposes this test's workspace.</summary>
    public Task DisposeAsync()
    {
        Workspace.Dispose();
        return Task.CompletedTask;
    }
}
