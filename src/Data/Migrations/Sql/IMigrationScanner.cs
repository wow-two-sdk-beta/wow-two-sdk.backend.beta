namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Sql;

/// <summary>Defines the contract for parsing and validating raw migrations into an ordered, checksummed list.</summary>
public interface IMigrationScanner
{
    /// <summary>Reads the source, parses <c>NNN-name</c>, computes checksums, and returns migrations ordered by ordinal.</summary>
    /// <exception cref="InvalidOperationException">A folder name is malformed or two migrations share an ordinal.</exception>
    IReadOnlyList<MigrationDescriptor> Scan();
}
