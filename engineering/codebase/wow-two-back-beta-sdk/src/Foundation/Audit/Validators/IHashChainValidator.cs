namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit.Validators;

/// <summary>Defines validation of the consistency of a supplied hash chain.</summary>
/// <remarks>An intact result does not prove completeness or authenticity: no trusted external checkpoint is checked, so tail truncation or a consistently rewritten chain can pass.</remarks>
/// <typeparam name="TEntry">The consumer's entry type, which must expose the chain fields.</typeparam>
public interface IHashChainValidator<in TEntry>
    where TEntry : IHashChainedEntry
{
    /// <summary>Validates the entries in chain order — recomputes each hash and checks each previous-hash link — returning the first break or an intact result.</summary>
    /// <param name="orderedEntries">The entries in ascending sequence order.</param>
    HashChainValidationResult Validate(IEnumerable<TEntry> orderedEntries);
}
