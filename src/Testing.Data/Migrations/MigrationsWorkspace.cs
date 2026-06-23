namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

/// <summary>A throwaway on-disk migrations root for one test (the <c>{root}/NNN-name/{Apply,Rollback}.sql</c> layout the filesystem source reads), under the OS temp dir and deleted on dispose.</summary>
/// <remarks>The source requires a Rollback.sql in every folder, so <see cref="Write"/> always writes both; pass an empty rollback when a migration is never rolled back in the test.</remarks>
public sealed class MigrationsWorkspace : IDisposable
{
    private const string ApplyFileName = "Apply.sql";
    private const string RollbackFileName = "Rollback.sql";

    /// <summary>The migrations root passed to <c>AddDatabaseBespokeMigrations</c>.</summary>
    public string Root { get; }

    /// <summary>Creates a fresh, empty migrations root under the OS temp directory.</summary>
    public MigrationsWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "wow2-migrations-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>Writes (or overwrites) a migration folder <c>NNN-name</c> with its Apply and Rollback scripts.</summary>
    /// <param name="folder">The <c>NNN-name</c> folder name, e.g. <c>001-baseline</c>.</param>
    /// <param name="applySql">The Apply script body.</param>
    /// <param name="rollbackSql">The Rollback script body (every folder must ship one).</param>
    public void Write(string folder, string applySql, string rollbackSql)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(applySql);
        ArgumentNullException.ThrowIfNull(rollbackSql);

        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ApplyFileName), applySql);
        File.WriteAllText(Path.Combine(dir, RollbackFileName), rollbackSql);
    }

    /// <summary>Overwrites just the Apply script of an existing folder to simulate drift on disk.</summary>
    /// <param name="folder">The existing <c>NNN-name</c> folder to mutate.</param>
    /// <param name="applySql">The replacement Apply script body.</param>
    public void OverwriteApply(string folder, string applySql)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(applySql);

        File.WriteAllText(Path.Combine(Root, folder, ApplyFileName), applySql);
    }

    /// <summary>Deletes a migration folder from the source to simulate an orphaned history row.</summary>
    /// <param name="folder">The <c>NNN-name</c> folder to remove.</param>
    public void DeleteFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        Directory.Delete(Path.Combine(Root, folder), recursive: true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup — a leaked temp dir must never fail a test.
        }
    }
}
