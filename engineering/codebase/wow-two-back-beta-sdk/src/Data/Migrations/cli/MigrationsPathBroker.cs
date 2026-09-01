using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Cli;

/// <summary>Locates the migrations directory by walking up from the cwd and the binary directory.</summary>
internal sealed class MigrationsPathBroker : IMigrationsPathBroker
{
    /// <inheritdoc />
    public Result<string> Locate()
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
                        return Result<string>.Ok(candidate);
                }
            }
        }

        return Result<string>.Fail(AppErrorFactory.FileNotFound(
            "Could not locate the Migrations directory. Pass --sql-dir <path> or set WOW_MIGRATE_SQL_DIR."));
    }
}
