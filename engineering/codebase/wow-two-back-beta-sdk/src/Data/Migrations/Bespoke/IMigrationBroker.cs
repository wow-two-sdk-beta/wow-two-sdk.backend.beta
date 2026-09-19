using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Defines reading raw migration SQL from a backing store.</summary>
/// <remarks>Use a filesystem source for the CLI and dev; use an embedded-resource source for self-contained runtime deploys.</remarks>
public interface IMigrationBroker
{
    /// <summary>Reads every numbered migration (folders matching <c>NNN-name</c>) with its Apply and Rollback SQL.</summary>
    /// <returns>The migrations, or an error when the source is incomplete or unreadable.</returns>
    Result<IReadOnlyList<RawMigration>> Read();
}
