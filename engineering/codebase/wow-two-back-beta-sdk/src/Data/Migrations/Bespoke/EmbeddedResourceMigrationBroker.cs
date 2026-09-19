using System.Reflection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Integrates migration scripts stored as assembly resources.</summary>
/// <remarks>Use at runtime so the schema ships inside the binary — no filesystem dependency at deploy.</remarks>
public sealed class EmbeddedResourceMigrationBroker(Assembly assembly, string folderPrefix = "Migrations/") : IMigrationBroker
{
    /// <inheritdoc />
    public Result<IReadOnlyList<RawMigration>> Read()
    {
        var apply = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rollback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var normalized = resourceName.Replace('\\', '/');
            if (!normalized.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase) ||
                !normalized.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = normalized[folderPrefix.Length..]; // e.g. "001-baseline/Apply.sql"
            var slash = relative.IndexOf('/');
            if (slash <= 0)
                continue;

            var folder = relative[..slash];
            var file = relative[(slash + 1)..];

            // Bucket each resource by folder into apply / rollback.
            if (file.Equals(MigrationConstants.ApplyFileName, StringComparison.OrdinalIgnoreCase))
            {
                if (ReadResource(resourceName).IsFailure(out var applyError, out var applyResourceSql))
                    return Result<IReadOnlyList<RawMigration>>.Fail(applyError);

                apply[folder] = applyResourceSql;
            }
            else if (file.Equals(MigrationConstants.RollbackFileName, StringComparison.OrdinalIgnoreCase))
            {
                if (ReadResource(resourceName).IsFailure(out var rollbackError, out var rollbackResourceSql))
                    return Result<IReadOnlyList<RawMigration>>.Fail(rollbackError);

                rollback[folder] = rollbackResourceSql;
            }
        }

        var migrations = new List<RawMigration>(apply.Count);
        foreach (var (folder, applySql) in apply)
        {
            if (!rollback.TryGetValue(folder, out var rollbackSql))
            {
                return Result<IReadOnlyList<RawMigration>>.Fail(AppErrorFactory.FileNotFound(
                    $"Migration '{folder}' is missing {MigrationConstants.RollbackFileName}."));
            }

            migrations.Add(new RawMigration
            {
                Name = folder,
                ApplySql = applySql,
                RollbackSql = rollbackSql,
            });
        }

        return Result<IReadOnlyList<RawMigration>>.Ok(migrations);
    }

    private Result<string> ReadResource(string name)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                return Result<string>.Fail(AppErrorFactory.DataIntegrity(
                    $"Embedded migration resource '{name}' could not be opened."));
            }

            using var reader = new StreamReader(stream);
            return Result<string>.Ok(reader.ReadToEnd());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<string>.Fail(AppErrorFactory.DataIntegrity(
                $"Embedded migration resource '{name}' could not be read.", exception));
        }
    }
}
