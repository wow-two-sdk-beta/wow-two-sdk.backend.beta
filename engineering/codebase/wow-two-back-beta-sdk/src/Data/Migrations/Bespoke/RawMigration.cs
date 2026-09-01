namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Represents a migration as discovered by an <see cref="IMigrationBroker"/>, pre-validation.</summary>
public sealed record RawMigration
{
    /// <summary>Gets the folder name, expected to match <c>NNN-name</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the Apply script contents.</summary>
    public required string ApplySql { get; init; }

    /// <summary>Gets the Rollback script contents — every migration ships one.</summary>
    public required string RollbackSql { get; init; }
}
