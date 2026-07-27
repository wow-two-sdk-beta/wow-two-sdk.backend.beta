using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Provides hash-chain verification — replays the chain, recomputing every hash and checking every link to detect tampering.</summary>
/// <remarks>Walks the entries once in order — for each it checks the sequence is gap-free, the previous-hash matches the prior entry's hash, and the stored hash equals the hash recomputed from the same SDK-plus-consumer payload the sealer used. Stops and reports the first break.</remarks>
/// <typeparam name="TEntry">The consumer's entry type, which must expose the chain fields.</typeparam>
public sealed class HashChainVerifier<TEntry> : IHashChainVerifier<TEntry>
    where TEntry : IHashChainedEntry
{
    private readonly IChainedEntryCanonicalizer<TEntry> _canonicalizer;
    private readonly HashChainAlgorithm _algorithm;

    /// <summary>Initializes the verifier with the consumer canonicalizer and the configured options.</summary>
    /// <param name="canonicalizer">The consumer projection that must match the one used to seal the chain, including its scheme version.</param>
    /// <param name="options">The hash-chain options carrying the algorithm choice.</param>
    public HashChainVerifier(IChainedEntryCanonicalizer<TEntry> canonicalizer, IOptions<HashChainOptions> options)
    {
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(options);

        _canonicalizer = canonicalizer;
        _algorithm = options.Value.Algorithm;
    }

    /// <inheritdoc />
    public HashChainVerificationResult Verify(IEnumerable<TEntry> orderedEntries)
    {
        ArgumentNullException.ThrowIfNull(orderedEntries);

        var index = 0;
        long expectedSequence = 1;
        byte[] expectedPreviousHash = [];

        foreach (var entry in orderedEntries)
        {
            ArgumentNullException.ThrowIfNull(entry);

            // The sequence must advance by exactly one — a gap, duplicate, or reorder shows here.
            if (entry.Sequence != expectedSequence)
            {
                return HashChainVerificationResult.Broken(HashChainBreakReason.SequenceGap, entry.Sequence, index);
            }

            // The link must point at the prior entry's hash — an insert or delete breaks it.
            // Constant-time compare per the standard (no early-out that leaks the diff position).
            if (!CryptographicOperations.FixedTimeEquals(entry.PreviousHash, expectedPreviousHash))
            {
                return HashChainVerificationResult.Broken(HashChainBreakReason.BrokenLink, entry.Sequence, index);
            }

            // Recompute from the same payload the sealer hashed — any altered field shows here.
            var recomputed = HashChainHasher.Compute(entry, _canonicalizer, _algorithm, entry.Sequence, entry.PreviousHash);
            if (!CryptographicOperations.FixedTimeEquals(entry.Hash, recomputed))
            {
                return HashChainVerificationResult.Broken(HashChainBreakReason.HashMismatch, entry.Sequence, index);
            }

            expectedPreviousHash = entry.Hash;
            expectedSequence++;
            index++;
        }

        return HashChainVerificationResult.Intact;
    }
}
