namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Thrown when an already-applied migration's source content has changed since it was applied.</summary>
public sealed class MigrationDriftException(IReadOnlyList<string> drifted)
    : Exception($"Applied migrations changed in the source (checksum drift): {string.Join(", ", drifted)}. " +
                "Revert the edits, or re-record the checksums via the migrator's repair operation to accept them.")
{
    /// <summary>Gets the drifted migration labels (<c>NNN-name</c>).</summary>
    public IReadOnlyList<string> Drifted { get; } = drifted;
}
