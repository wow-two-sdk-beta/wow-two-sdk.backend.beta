using System.Reflection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Sql;

/// <summary>Provides migrations embedded as assembly resources (logical name <c>Migrations/NNN-name/Apply.sql</c>).</summary>
/// <remarks>Use at runtime so the schema ships inside the binary — no filesystem dependency at deploy.</remarks>
public sealed class EmbeddedResourceMigrationSource(Assembly assembly, string folderPrefix = "Migrations/") : IMigrationSource
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">A migration is missing its embedded Rollback resource, or a resource stream cannot be opened.</exception>
    public IReadOnlyList<RawMigration> Read()
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
            if (file.Equals(MigrationConventions.ApplyFileName, StringComparison.OrdinalIgnoreCase))
                apply[folder] = ReadResource(resourceName);
            else if (file.Equals(MigrationConventions.RollbackFileName, StringComparison.OrdinalIgnoreCase))
                rollback[folder] = ReadResource(resourceName);
        }

        return apply
            .Select(kv => new RawMigration
            {
                Name = kv.Key,
                ApplySql = kv.Value,
                RollbackSql = GetRollbackOrThrow(rollback, kv.Key),
            })
            .ToList();
    }

    private static string GetRollbackOrThrow(Dictionary<string, string> rollback, string folder) =>
        rollback.TryGetValue(folder, out var sql)
            ? sql
            : throw new InvalidOperationException(
                $"Migration '{folder}' is missing {MigrationConventions.RollbackFileName} — every migration must ship a rollback.");

    private string ReadResource(string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded migration resource '{name}' could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
