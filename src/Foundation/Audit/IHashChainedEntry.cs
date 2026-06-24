namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Defines an entry that participates in a tamper-evident SHA-256 hash chain.</summary>
/// <remarks>The only shape the SDK locks. A consumer supplies its own domain fields and tells the SDK which of them to hash via an <see cref="IChainedEntryCanonicalizer{TEntry}"/>; these three fields carry the chain itself and are stamped by the <see cref="IHashChainSealer{TEntry}"/>.</remarks>
public interface IHashChainedEntry
{
    /// <summary>Gets or sets the monotonic, gap-free position of this entry in the chain, starting at 1.</summary>
    long Sequence { get; set; }

    /// <summary>Gets or sets the hash of the entry immediately before this one, empty for the genesis entry — linking the chain backwards.</summary>
    byte[] PreviousHash { get; set; }

    /// <summary>Gets or sets the hash computed over this entry's chain fields and canonical payload — the value the next entry links to.</summary>
    byte[] Hash { get; set; }
}
