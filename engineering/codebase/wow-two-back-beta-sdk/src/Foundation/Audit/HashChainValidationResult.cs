namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Represents the outcome of validating the consistency of a supplied hash chain.</summary>
public sealed record HashChainValidationResult
{
    private HashChainValidationResult()
    {
    }

    /// <summary>Gets a value indicating whether the supplied entries form a consistent chain.</summary>
    public bool IsIntact => Reason == HashChainBreakReason.None;

    /// <summary>Gets the reason the chain broke, or <see cref="HashChainBreakReason.None"/> when intact.</summary>
    public HashChainBreakReason Reason { get; private init; }

    /// <summary>Gets the sequence of the first broken entry, or <see langword="null"/> when the chain is intact.</summary>
    public long? BrokenSequence { get; private init; }

    /// <summary>Gets the zero-based position of the first broken entry within the validated collection, or <see langword="null"/> when intact.</summary>
    public int? BrokenIndex { get; private init; }

    /// <summary>Represents a consistent supplied chain.</summary>
    public static HashChainValidationResult Intact { get; } = new() { Reason = HashChainBreakReason.None };

    /// <summary>Creates a result describing the first broken entry.</summary>
    /// <param name="reason">Why the chain broke.</param>
    /// <param name="brokenSequence">The sequence of the offending entry.</param>
    /// <param name="brokenIndex">The zero-based position of the offending entry in the validated collection.</param>
    public static HashChainValidationResult Broken(HashChainBreakReason reason, long brokenSequence, int brokenIndex)
    {
        return new HashChainValidationResult
        {
            Reason = reason,
            BrokenSequence = brokenSequence,
            BrokenIndex = brokenIndex,
        };
    }
}
