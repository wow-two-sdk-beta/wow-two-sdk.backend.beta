namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Represents a validated, ordered migration with parsed ordinal, name, checksum, and execution flags.</summary>
/// <example>The baseline migration ready to apply.</example>
public sealed record MigrationDescriptor
{
    /// <summary>Gets the numeric ordinal parsed from the <c>NNN-</c> folder prefix — the apply gate and ordering key.</summary>
    public required int Ordinal { get; init; }

    /// <summary>Gets the descriptive name (folder text after <c>NNN-</c>).</summary>
    /// <example>baseline</example>
    public required string Name { get; init; }

    /// <summary>Gets the Apply script body.</summary>
    /// <example>CREATE TABLE statements</example>
    public required string ApplySql { get; init; }

    /// <summary>Gets the Rollback script body — every migration ships one.</summary>
    /// <example>DROP TABLE statements</example>
    public required string RollbackSql { get; init; }

    /// <summary>Gets the SHA-256 over the normalized Apply script.</summary>
    /// <example>SHA-256 hex digest</example>
    public required string Checksum { get; init; }

    /// <summary>Gets whether the Apply script runs outside a transaction, declared via a leading no-transaction directive.</summary>
    /// <remarks>When true the script is recorded in a separate statement, so a crash mid-apply re-runs it — the Apply SQL MUST be idempotent (e.g. <c>CREATE INDEX CONCURRENTLY IF NOT EXISTS</c>, guarded <c>DO</c> blocks).</remarks>
    /// <example>true for a CREATE INDEX CONCURRENTLY migration</example>
    public required bool NoTransaction { get; init; }

    /// <summary>Gets the <c>NNN-name</c> label for logs and status.</summary>
    /// <example>001-baseline</example>
    public string Label => $"{Ordinal:D3}-{Name}";
}
