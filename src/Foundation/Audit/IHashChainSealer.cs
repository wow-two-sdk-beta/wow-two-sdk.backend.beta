namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Defines the contract for stamping an entry's chain fields so it links to the one before it.</summary>
/// <typeparam name="TEntry">The consumer's entry type, which must expose the chain fields.</typeparam>
public interface IHashChainSealer<TEntry>
    where TEntry : IHashChainedEntry
{
    /// <summary>Seals <paramref name="entry"/> against <paramref name="previous"/> — sets <see cref="IHashChainedEntry.Sequence"/>, <see cref="IHashChainedEntry.PreviousHash"/>, and <see cref="IHashChainedEntry.Hash"/>.</summary>
    /// <param name="entry">The entry to stamp; its domain fields must be fully populated before sealing.</param>
    /// <param name="previous">The last sealed entry in the chain, or <see langword="null"/> for the genesis entry.</param>
    void Seal(TEntry entry, TEntry? previous);
}
