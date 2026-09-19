using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Integrates migration scripts stored on the local filesystem.</summary>
public sealed class FileSystemMigrationBroker(string migrationsRoot) : IMigrationBroker
{
    /// <summary>Gets the migrations root — the folder containing the <c>NNN-name</c> directories.</summary>
    public string Root { get; } = migrationsRoot;

    /// <inheritdoc />
    public Result<IReadOnlyList<RawMigration>> Read()
    {
        try
        {
            if (!Directory.Exists(Root))
                return Result<IReadOnlyList<RawMigration>>.Ok([]);

            var migrations = new List<RawMigration>();
            foreach (var dir in Directory.GetDirectories(Root))
            {
                var applyPath = Path.Combine(dir, MigrationConstants.ApplyFileName);
                if (!File.Exists(applyPath))
                    continue; // Skip the Dev folder and anything without an Apply script.

                var rollbackPath = Path.Combine(dir, MigrationConstants.RollbackFileName);
                if (!File.Exists(rollbackPath))
                {
                    return Result<IReadOnlyList<RawMigration>>.Fail(AppErrorFactory.FileNotFound(
                        $"Migration '{Path.GetFileName(dir)}' is missing {MigrationConstants.RollbackFileName}."));
                }

                migrations.Add(new RawMigration
                {
                    Name = Path.GetFileName(dir),
                    ApplySql = File.ReadAllText(applyPath),
                    RollbackSql = File.ReadAllText(rollbackPath),
                });
            }

            return Result<IReadOnlyList<RawMigration>>.Ok(migrations);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<IReadOnlyList<RawMigration>>.Fail(AppErrorFactory.DataIntegrity(
                $"Migration source '{Root}' could not be read.", exception));
        }
    }
}
