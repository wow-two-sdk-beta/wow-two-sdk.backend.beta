namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

/// <summary>A projected <c>migration_history</c> row for test assertions.</summary>
/// <remarks>A settable-property class (not a record) so Dapper leniently converts SQLite's <c>Int64</c> ordinal to <c>int</c>; a record's ctor matching is strict on the column type and would reject it.</remarks>
public sealed class MigrationHistoryRow
{
    /// <summary>The migration ordinal.</summary>
    public int Ordinal { get; set; }

    /// <summary>The migration name (folder suffix).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The recorded source checksum.</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>The host stamp recorded on the row.</summary>
    public string AppliedBy { get; set; } = string.Empty;
}
