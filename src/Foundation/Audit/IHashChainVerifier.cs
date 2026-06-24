namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Defines the contract for replaying a hash chain to prove no entry was altered, inserted, removed, or reordered.</summary>
/// <typeparam name="TEntry">The consumer's entry type, which must expose the chain fields.</typeparam>
public interface IHashChainVerifier<in TEntry>
    where TEntry : IHashChainedEntry
{
    /// <summary>Verifies the entries in chain order — recomputes each hash and checks each previous-hash link — returning the first break or an intact result.</summary>
    /// <param name="orderedEntries">The entries in ascending sequence order.</param>
    HashChainVerificationResult Verify(IEnumerable<TEntry> orderedEntries);
}
