namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

/// <summary>
/// A throwaway on-disk migrations root for one test — the <c>{root}/NNN-name/{Apply,Rollback}.sql</c> layout the
/// filesystem migration source reads. Created under the OS temp dir and deleted on <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// Mirrors how a real product ships migrations on disk, but every folder is written by the test so the SQL is
/// trivial (e.g. <c>create table t1(id int primary key)</c>). The source REQUIRES a Rollback.sql in every folder
/// (it throws otherwise), so <see cref="Write"/> always writes both files; pass an empty rollback only when the
/// test never rolls that migration back.
/// </remarks>
public sealed class MigrationsWorkspace : IDisposable
{
    private const string ApplyFileName = "Apply.sql";
    private const string RollbackFileName = "Rollback.sql";

    /// <summary>The migrations root passed to <c>AddDatabaseBespokeMigrations</c>.</summary>
    public string Root { get; }

    /// <summary>Creates a fresh, empty migrations root under the OS temp directory.</summary>
    public MigrationsWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "wow2-mig-sqlite-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>Writes (or overwrites) a migration folder <c>NNN-name</c> with its Apply and Rollback scripts.</summary>
    /// <param name="folder">The <c>NNN-name</c> folder name, e.g. <c>001-baseline</c>.</param>
    /// <param name="applySql">The Apply script body.</param>
    /// <param name="rollbackSql">The Rollback script body (every folder must ship one).</param>
    public void Write(string folder, string applySql, string rollbackSql)
    {
        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ApplyFileName), applySql);
        File.WriteAllText(Path.Combine(dir, RollbackFileName), rollbackSql);
    }

    /// <summary>Overwrites just the Apply script of an existing folder — used to simulate drift on disk.</summary>
    public void OverwriteApply(string folder, string applySql) =>
        File.WriteAllText(Path.Combine(Root, folder, ApplyFileName), applySql);

    /// <summary>Deletes a migration folder from the source — used to simulate an orphaned history row.</summary>
    public void DeleteFolder(string folder) =>
        Directory.Delete(Path.Combine(Root, folder), recursive: true);

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
