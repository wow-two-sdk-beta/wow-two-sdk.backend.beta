using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit.Validators;

/// <summary>Validates the consistency of a supplied hash chain.</summary>
/// <remarks>Walks the entries once in order — for each it checks the sequence is gap-free, the previous-hash matches the prior entry's hash, and the stored hash equals the hash recomputed from the same SDK-plus-consumer payload the sealer used. Stops and reports the first break. An intact result establishes consistency of the supplied entries only; no trusted checkpoint is checked, so tail truncation or a consistently rewritten chain can pass.</remarks>
/// <typeparam name="TEntry">The consumer's entry type, which must expose the chain fields.</typeparam>
public sealed class HashChainValidator<TEntry> : IHashChainValidator<TEntry>
    where TEntry : IHashChainedEntry
{
    private readonly HashChainHasher _hasher = new();
    private readonly IChainedEntryCanonicalizer<TEntry> _canonicalizer;
    private readonly HashChainAlgorithm _algorithm;

    /// <summary>Initializes the validator with the consumer canonicalizer and the configured options.</summary>
    /// <param name="canonicalizer">The consumer projection that must match the one used to seal the chain, including its scheme version.</param>
    /// <param name="options">The hash-chain options carrying the algorithm choice.</param>
    public HashChainValidator(IChainedEntryCanonicalizer<TEntry> canonicalizer, HashChainOptions options)
    {
        ArgumentNullException.ThrowIfNull(canonicalizer);
        ArgumentNullException.ThrowIfNull(options);

        _canonicalizer = canonicalizer;
        _algorithm = options.Algorithm;
    }

    /// <inheritdoc />
    public HashChainValidationResult Validate(IEnumerable<TEntry> orderedEntries)
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
                return HashChainValidationResult.Broken(HashChainBreakReason.SequenceGap, entry.Sequence, index);
            }

            // Compare the supplied prior hash in constant time.
            if (!CryptographicOperations.FixedTimeEquals(entry.PreviousHash, expectedPreviousHash))
            {
                return HashChainValidationResult.Broken(HashChainBreakReason.BrokenLink, entry.Sequence, index);
            }

            // Recompute from the same payload the sealer hashed and compare it with the supplied hash.
            var recomputed = _hasher.Compute(entry, _canonicalizer, _algorithm, entry.Sequence, entry.PreviousHash);
            if (!CryptographicOperations.FixedTimeEquals(entry.Hash, recomputed))
            {
                return HashChainValidationResult.Broken(HashChainBreakReason.HashMismatch, entry.Sequence, index);
            }

            expectedPreviousHash = entry.Hash;
            expectedSequence++;
            index++;
        }

        return HashChainValidationResult.Intact;
    }
}
