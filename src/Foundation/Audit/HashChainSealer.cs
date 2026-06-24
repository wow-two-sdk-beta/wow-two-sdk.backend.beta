using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Provides hash-chain sealing — stamps each entry's sequence, previous-hash link, and hash before it is persisted.</summary>
/// <remarks>The hashed payload is the SDK chain fields (scheme version, sequence, previous hash) followed by the consumer canonicalizer's fields, so tamper-evidence holds no matter which domain fields the consumer chooses.</remarks>
/// <typeparam name="TEntry">The consumer's entry type, which must expose the chain fields.</typeparam>
public sealed class HashChainSealer<TEntry> : IHashChainSealer<TEntry>
    where TEntry : IHashChainedEntry
{
    private readonly IChainedEntryCanonicalizer<TEntry> _canonicalizer;
    private readonly HashChainAlgorithm _algorithm;

    /// <summary>Initializes the sealer with the consumer canonicalizer and the configured options.</summary>
    /// <param name="canonicalizer">The consumer projection of an entry's domain fields onto the canonical payload.</param>
    /// <param name="options">The hash-chain options carrying the algorithm choice.</param>
    public HashChainSealer(IChainedEntryCanonicalizer<TEntry> canonicalizer, IOptions<HashChainOptions> options)
    {
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(options);

        _canonicalizer = canonicalizer;
        _algorithm = options.Value.Algorithm;
    }

    /// <inheritdoc />
    public void Seal(TEntry entry, TEntry? previous)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // The genesis entry starts at sequence 1 with an empty previous hash; every later entry links to its predecessor.
        var sequence = (previous?.Sequence ?? 0) + 1;
        var previousHash = previous?.Hash ?? [];

        entry.Sequence = sequence;
        entry.PreviousHash = previousHash;
        entry.Hash = HashChainHasher.Compute(entry, _canonicalizer, _algorithm, sequence, previousHash);
    }
}
