namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Sql;

/// <summary>Thrown when the history records applied migrations whose source is absent — the running binary is older than the database, or a migration folder was deleted.</summary>
public sealed class MigrationOrphanException(IReadOnlyList<int> orphanedOrdinals)
    : Exception($"The database has applied migrations missing from this source (orphaned): " +
                $"{string.Join(", ", orphanedOrdinals.Select(o => o.ToString("D3")))}. " +
                "The running binary is likely older than the database, or a migration folder was deleted. " +
                "Deploy a binary whose source includes these ordinals, or set MigrationOptions.AllowOrphanedHistory to proceed.")
{
    /// <summary>Gets the ordinals present in the history but absent from the source.</summary>
    public IReadOnlyList<int> OrphanedOrdinals { get; } = orphanedOrdinals;
}
