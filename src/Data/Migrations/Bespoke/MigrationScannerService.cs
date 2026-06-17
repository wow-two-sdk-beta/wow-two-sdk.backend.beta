namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Provides parsing and validation of raw migrations into an ordered, checksummed list.</summary>
/// <remarks>Reads the source, parses the <c>NNN-name</c> prefix, computes a checksum per Apply script, and rejects malformed or duplicate ordinals.</remarks>
public sealed class MigrationScannerService(IMigrationSource source) : IMigrationScanner
{
    /// <inheritdoc />
    public IReadOnlyList<MigrationDescriptor> Scan()
    {
        var descriptors = new List<MigrationDescriptor>();

        foreach (var raw in source.Read())
        {
            // Parse the NNN-name prefix; reject anything that does not match.
            var match = MigrationConventions.FolderPattern().Match(raw.Name);
            if (!match.Success)
                throw new InvalidOperationException(
                    $"Migration folder '{raw.Name}' must match NNN-name (e.g. 001-baseline).");

            descriptors.Add(new MigrationDescriptor
            {
                Ordinal = int.Parse(match.Groups[1].Value),
                Name = match.Groups[2].Value,
                ApplySql = raw.ApplySql,
                RollbackSql = raw.RollbackSql,
                Checksum = raw.ApplySql.ToMigrationChecksum(),
                NoTransaction = HasNoTransactionDirective(raw.ApplySql),
            });
        }

        // Reject two migrations claiming the same ordinal.
        var duplicate = descriptors.GroupBy(d => d.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException(
                $"Duplicate migration ordinal {duplicate.Key:D3}: {string.Join(", ", duplicate.Select(d => d.Name))}.");

        return descriptors.OrderBy(d => d.Ordinal).ToList();
    }

    /// <summary>Gets whether the leading comment header contains a no-transaction directive.</summary>
    /// <param name="applySql">The Apply script to inspect.</param>
    private static bool HasNoTransactionDirective(string applySql)
    {
        using var reader = new StringReader(applySql);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;
            if (!trimmed.StartsWith("--", StringComparison.Ordinal))
                break; // The first non-comment line ends the header.
            if (trimmed.Replace(" ", "").Equals(MigrationConventions.NoTransactionDirectiveCompact, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
