namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Represents a snapshot of migrator state: what is applied, pending, drifted, or orphaned.</summary>
public sealed record MigrationStatus
{
    /// <summary>Gets the migrations recorded in the database, ordered by ordinal.</summary>
    public required IReadOnlyList<MigrationHistoryEntry> Applied { get; init; }

    /// <summary>Gets the migrations in the source but not yet applied.</summary>
    public required IReadOnlyList<MigrationDescriptor> Pending { get; init; }

    /// <summary>Gets the applied migrations whose current source checksum no longer matches what was recorded.</summary>
    public required IReadOnlyList<MigrationDescriptor> Drifted { get; init; }

    /// <summary>Gets the ordinals recorded in the database with no matching migration in the source.</summary>
    public required IReadOnlyList<int> Orphaned { get; init; }
}
