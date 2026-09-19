namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Holds configuration for the SQL migrator, shared by every host.</summary>
public sealed record MigrationOptions
{
    /// <summary>Gets or sets the version label stamped onto applied rows.</summary>
    public string Version { get; set; } = "v1.0";

    /// <summary>Gets or sets whether rollback and repair are permitted.</summary>
    public bool AllowRollback { get; set; }

    /// <summary>Gets or sets whether apply tolerates orphaned history (applied rows with no source migration).</summary>
    public bool AllowOrphanedHistory { get; set; }

    /// <summary>Gets or sets the database provider, which selects the SQL dialect.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Postgres;

    /// <summary>Gets or sets the schema that owns the migration-history table.</summary>
    public string SchemaName { get; set; } = "public";

    /// <summary>Gets or sets the migration-history table name.</summary>
    public string TableName { get; set; } = "migration_history";

    /// <summary>Gets or sets the advisory-lock id shared by every host so only one apply loop runs at a time.</summary>
    public long AdvisoryLockId { get; set; } = 4_855_178_001L;
}
