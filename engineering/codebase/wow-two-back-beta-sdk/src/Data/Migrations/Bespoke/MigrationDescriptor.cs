namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Represents a validated, ordered migration with parsed ordinal, name, checksum, and execution flags.</summary>
public sealed record MigrationDescriptor
{
    /// <summary>Gets the numeric ordinal parsed from the <c>NNN-</c> folder prefix — the apply gate and ordering key.</summary>
    public required int Ordinal { get; init; }

    /// <summary>Gets the descriptive name (folder text after <c>NNN-</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the Apply script body.</summary>
    public required string ApplySql { get; init; }

    /// <summary>Gets the Rollback script body — every migration ships one.</summary>
    public required string RollbackSql { get; init; }

    /// <summary>Gets the SHA-256 over the normalized Apply script.</summary>
    public required string Checksum { get; init; }

    /// <summary>Gets whether the Apply script runs outside a transaction, declared via a leading no-transaction directive.</summary>
    public required bool NoTransaction { get; init; }

    /// <summary>Gets the <c>NNN-name</c> label for logs and status.</summary>
    public string Label => $"{Ordinal:D3}-{Name}";
}
