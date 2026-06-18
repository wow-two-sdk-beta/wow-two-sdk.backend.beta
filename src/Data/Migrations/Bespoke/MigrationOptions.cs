namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Options for the SQL migrator, set in code via the <c>AddDatabaseBespokeMigrations</c> configure hook and shared by every host.</summary>
/// <remarks>This is a mutable options object (not an appsettings-bound settings record) — the CLI flips <see cref="AllowRollback"/> at registration.</remarks>
public sealed class MigrationOptions
{
    /// <summary>Gets or sets the version label stamped onto applied rows.</summary>
    /// <example>v1.0</example>
    public string Version { get; set; } = "v1.0";

    /// <summary>Gets or sets whether rollback and repair are permitted. Off by default; enable per-operation to permit a guarded rollback/repair — a recovery op usable in any environment, production included. Not an environment gate.</summary>
    /// <example>false</example>
    public bool AllowRollback { get; set; }

    /// <summary>Gets or sets whether apply tolerates orphaned history (applied rows with no source migration). Keep false to fail closed when the running binary is older than the database.</summary>
    /// <example>false</example>
    public bool AllowOrphanedHistory { get; set; }

    /// <summary>Gets or sets the database provider, which selects the SQL dialect.</summary>
    /// <example>Targets a Postgres database.</example>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Postgres;

    /// <summary>Gets or sets the schema that owns the migration-history table.</summary>
    /// <remarks>Ignored by providers without schemas, such as SQLite.</remarks>
    /// <example>public</example>
    public string SchemaName { get; set; } = "public";

    /// <summary>Gets or sets the migration-history table name.</summary>
    /// <example>migration_history</example>
    public string TableName { get; set; } = "migration_history";

    /// <summary>Gets or sets the advisory-lock id shared by every host so only one apply loop runs at a time.</summary>
    /// <remarks>Used only by providers with advisory locks (Postgres); SQLite serializes via a busy-timeout instead.</remarks>
    /// <example>4855178001</example>
    public long AdvisoryLockId { get; set; } = 4_855_178_001L;
}
