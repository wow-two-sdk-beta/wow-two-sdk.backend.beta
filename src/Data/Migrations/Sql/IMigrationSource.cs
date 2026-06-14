namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Sql;

/// <summary>Defines the contract for reading raw migration SQL from a backing store.</summary>
/// <remarks>Use a filesystem source for the CLI and dev; use an embedded-resource source for self-contained runtime deploys.</remarks>
public interface IMigrationSource
{
    /// <summary>Reads every numbered migration (folders matching <c>NNN-name</c>) with its Apply and Rollback SQL.</summary>
    /// <exception cref="InvalidOperationException">A migration is missing its Rollback script.</exception>
    IReadOnlyList<RawMigration> Read();
}
