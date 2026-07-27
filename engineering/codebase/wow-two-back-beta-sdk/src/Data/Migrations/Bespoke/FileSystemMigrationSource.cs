namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Provides migrations read from <c>{root}/NNN-name/{Apply,Rollback}.sql</c> on disk for the CLI and dev.</summary>
public sealed class FileSystemMigrationSource(string migrationsRoot) : IMigrationSource
{
    /// <summary>Gets the migrations root — the folder containing the <c>NNN-name</c> directories.</summary>
    public string Root { get; } = migrationsRoot;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">A migration folder is missing its Rollback script.</exception>
    public IReadOnlyList<RawMigration> Read()
    {
        if (!Directory.Exists(Root))
            return [];

        var migrations = new List<RawMigration>();
        foreach (var dir in Directory.GetDirectories(Root))
        {
            var applyPath = Path.Combine(dir, MigrationConventions.ApplyFileName);
            if (!File.Exists(applyPath))
                continue; // Skip the Dev folder and anything without an Apply script.

            var rollbackPath = Path.Combine(dir, MigrationConventions.RollbackFileName);
            if (!File.Exists(rollbackPath))
                throw new InvalidOperationException(
                    $"Migration '{Path.GetFileName(dir)}' is missing {MigrationConventions.RollbackFileName} — every migration must ship a rollback.");

            migrations.Add(new RawMigration
            {
                Name = Path.GetFileName(dir),
                ApplySql = File.ReadAllText(applyPath),
                RollbackSql = File.ReadAllText(rollbackPath),
            });
        }

        return migrations;
    }
}
