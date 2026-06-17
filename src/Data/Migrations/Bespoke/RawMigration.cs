namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Represents a migration as discovered by an <see cref="IMigrationSource"/>, pre-validation.</summary>
/// <example>A raw baseline migration with its Apply and Rollback SQL.</example>
public sealed record RawMigration
{
    /// <summary>Gets the folder name, expected to match <c>NNN-name</c>.</summary>
    /// <example>001-baseline</example>
    public required string Name { get; init; }

    /// <summary>Gets the Apply script contents.</summary>
    /// <example>CREATE TABLE statements</example>
    public required string ApplySql { get; init; }

    /// <summary>Gets the Rollback script contents — every migration ships one.</summary>
    /// <example>DROP TABLE statements</example>
    public required string RollbackSql { get; init; }
}
