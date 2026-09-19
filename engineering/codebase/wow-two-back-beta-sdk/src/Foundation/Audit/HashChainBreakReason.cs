namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Refers to why a hash-chain validation failed.</summary>
public enum HashChainBreakReason
{
    /// <summary>The chain validated intact — no break.</summary>
    None = 0,

    /// <summary>An entry's stored hash does not match the hash recomputed from its fields.</summary>
    HashMismatch,

    /// <summary>An entry's previous-hash link does not match the prior entry's hash.</summary>
    BrokenLink,

    /// <summary>An entry's sequence is not exactly one past its predecessor — a gap or duplicate in the chain.</summary>
    SequenceGap,
}
