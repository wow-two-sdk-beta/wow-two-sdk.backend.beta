using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

/// <summary>
/// Base for SQLite migrator tests — owns a throwaway temp DB file and a fresh <see cref="MigrationsWorkspace"/>,
/// both deleted on dispose. No shared fixture: SQLite is in-process, so every test gets its own isolated file.
/// The migrator harness + workspace come from the SDK Testing.Data package (the SDK dogfoods its own extraction).
/// </summary>
public abstract class SqliteMigratorTestBase : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), "wow2-mig-sqlite-tests", Guid.NewGuid().ToString("N") + ".db");

    /// <summary>This test's throwaway on-disk migrations root.</summary>
    protected MigrationsWorkspace Workspace { get; } = new();

    /// <summary>The SQLite connection string (pooling off so the file is unlocked for clean teardown).</summary>
    protected string ConnectionString => $"Data Source={_databasePath};Pooling=False";

    /// <summary>Builds a migrator over this test's workspace + DB file.</summary>
    protected MigratorHarness CreateMigrator(Action<MigrationOptions>? configure = null) =>
        MigratorHarness.CreateSqlite(ConnectionString, Workspace.Root, configure);

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        Workspace.Dispose();
        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch
        {
            // Best-effort cleanup — a leaked temp DB must never fail a test.
        }

        return Task.CompletedTask;
    }
}
