namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Defines behavior that hashes a migration body into the stable checksum that detects drift in applied migrations.</summary>
public interface IMigrationChecksumHasher
{
    /// <summary>Computes the checksum of <paramref name="content"/> after normalizing line endings and trailing whitespace, so cross-machine churn never reads as drift.</summary>
    /// <param name="content">The migration SQL body to hash.</param>
    string Hash(string content);
}
