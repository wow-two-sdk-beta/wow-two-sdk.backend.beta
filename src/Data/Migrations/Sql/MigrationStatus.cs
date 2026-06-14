namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Sql;

/// <summary>Represents a snapshot of migrator state: what is applied, pending, drifted, or orphaned.</summary>
/// <example>A status report with one applied migration and none pending.</example>
public sealed record MigrationStatus
{
    /// <summary>Gets the migrations recorded in the database, ordered by ordinal.</summary>
    /// <example>Collection of applied migrations</example>
    public required IReadOnlyList<MigrationHistoryEntry> Applied { get; init; }

    /// <summary>Gets the migrations in the source but not yet applied.</summary>
    /// <example>Collection of pending migrations</example>
    public required IReadOnlyList<MigrationDescriptor> Pending { get; init; }

    /// <summary>Gets the applied migrations whose current source checksum no longer matches what was recorded.</summary>
    /// <example>Collection of drifted migrations</example>
    public required IReadOnlyList<MigrationDescriptor> Drifted { get; init; }

    /// <summary>Gets the ordinals recorded in the database with no matching migration in the source.</summary>
    /// <example>Collection of orphaned ordinals</example>
    public required IReadOnlyList<int> Orphaned { get; init; }
}
