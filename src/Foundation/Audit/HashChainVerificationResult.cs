namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Defines why a hash-chain verification failed.</summary>
public enum HashChainBreakReason
{
    /// <summary>The chain verified intact — no break.</summary>
    None = 0,

    /// <summary>An entry's stored hash does not match the hash recomputed from its fields — the entry was altered.</summary>
    HashMismatch,

    /// <summary>An entry's previous-hash link does not match the prior entry's hash — an entry was inserted, removed, or reordered.</summary>
    BrokenLink,

    /// <summary>An entry's sequence is not exactly one past its predecessor — a gap or duplicate in the chain.</summary>
    SequenceGap,
}

/// <summary>Represents the outcome of verifying a hash chain — intact, or the first entry where it broke and why.</summary>
public sealed record HashChainVerificationResult
{
    private HashChainVerificationResult()
    {
    }

    /// <summary>Gets a value indicating whether the whole chain verified intact.</summary>
    public bool IsIntact => Reason == HashChainBreakReason.None;

    /// <summary>Gets the reason the chain broke, or <see cref="HashChainBreakReason.None"/> when intact.</summary>
    public HashChainBreakReason Reason { get; private init; }

    /// <summary>Gets the sequence of the first broken entry, or <see langword="null"/> when the chain is intact.</summary>
    public long? BrokenSequence { get; private init; }

    /// <summary>Gets the zero-based position of the first broken entry within the verified collection, or <see langword="null"/> when intact.</summary>
    public int? BrokenIndex { get; private init; }

    /// <summary>Represents an intact chain.</summary>
    public static HashChainVerificationResult Intact { get; } = new() { Reason = HashChainBreakReason.None };

    /// <summary>Creates a result describing the first broken entry.</summary>
    /// <param name="reason">Why the chain broke.</param>
    /// <param name="brokenSequence">The sequence of the offending entry.</param>
    /// <param name="brokenIndex">The zero-based position of the offending entry in the verified collection.</param>
    public static HashChainVerificationResult Broken(HashChainBreakReason reason, long brokenSequence, int brokenIndex)
    {
        return new HashChainVerificationResult
        {
            Reason = reason,
            BrokenSequence = brokenSequence,
            BrokenIndex = brokenIndex,
        };
    }
}
