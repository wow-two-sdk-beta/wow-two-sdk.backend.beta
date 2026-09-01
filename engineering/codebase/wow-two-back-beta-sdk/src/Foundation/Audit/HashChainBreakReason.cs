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
