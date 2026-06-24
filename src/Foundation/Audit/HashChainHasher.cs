using System.Security.Cryptography;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Computes an entry's chain hash by prepending the SDK-owned chain fields, then the consumer's canonical payload.</summary>
/// <remarks>Single source of the hashing recipe — both <see cref="HashChainSealer{TEntry}"/> and <see cref="HashChainVerifier{TEntry}"/> route through it so seal and verify can never diverge. The prepended order is fixed — scheme version, sequence, previous hash — so a consumer canonicalizer cannot omit a chain field and silently weaken the proof.</remarks>
internal static class HashChainHasher
{
    /// <summary>Computes the hash for an entry positioned at <paramref name="sequence"/> after <paramref name="previousHash"/>.</summary>
    /// <typeparam name="TEntry">The consumer's entry type whose domain fields are hashed.</typeparam>
    /// <param name="entry">The entry whose domain fields are appended after the chain fields.</param>
    /// <param name="canonicalizer">The consumer projection of the entry's domain fields.</param>
    /// <param name="algorithm">The hash function to apply.</param>
    /// <param name="sequence">The chain position to bind into the hash.</param>
    /// <param name="previousHash">The prior entry's hash to bind into the hash, empty for the genesis entry.</param>
    public static byte[] Compute<TEntry>(
        TEntry entry,
        IChainedEntryCanonicalizer<TEntry> canonicalizer,
        HashChainAlgorithm algorithm,
        long sequence,
        byte[] previousHash)
    {
        var builder = new CanonicalPayloadBuilder();

        // SDK-owned chain fields come first and are never delegated — this is what makes omission impossible.
        builder.Append(canonicalizer.SchemeVersion);
        builder.Append(sequence);
        builder.Append(previousHash);

        // Then the consumer's chosen domain fields, in the consumer's fixed order.
        canonicalizer.Write(entry, builder);

        return HashData(algorithm, builder.AsSpan());
    }

    private static byte[] HashData(HashChainAlgorithm algorithm, ReadOnlySpan<byte> payload)
    {
        return algorithm switch
        {
            HashChainAlgorithm.Sha256 => SHA256.HashData(payload),
            HashChainAlgorithm.Sha384 => SHA384.HashData(payload),
            HashChainAlgorithm.Sha512 => SHA512.HashData(payload),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported hash-chain algorithm."),
        };
    }
}
