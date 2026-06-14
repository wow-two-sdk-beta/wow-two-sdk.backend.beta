namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Cli;

/// <summary>Locates the on-disk migrations directory for the filesystem source.</summary>
internal static class MigrationsPathResolver
{
    /// <summary>Walks up from the cwd (and the binary dir) looking for the migrations folder.</summary>
    /// <exception cref="DirectoryNotFoundException">No migrations folder found — caller should pass <c>--sql-dir</c>.</exception>
    public static string Resolve()
    {
        var relativeCandidates = new[]
        {
            Path.Combine("SqlFiles", "Migrations"),
            "Migrations",
        };

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                foreach (var relative in relativeCandidates)
                {
                    var candidate = Path.Combine(dir.FullName, relative);
                    if (Directory.Exists(candidate))
                        return candidate;
                }
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Migrations directory. Pass --sql-dir <path> or set WOW_MIGRATE_SQL_DIR.");
    }
}
