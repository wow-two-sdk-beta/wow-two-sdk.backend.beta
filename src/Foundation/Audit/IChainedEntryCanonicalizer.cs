namespace WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

/// <summary>Defines the consumer-supplied projection of an entry's domain fields onto the canonical payload that gets hashed.</summary>
/// <remarks>The SDK names no domain field. The consumer decides exactly which fields are part of the tamper-evident proof — and in which order — by appending them to the builder. The chain fields (<see cref="IHashChainedEntry.Sequence"/> and <see cref="IHashChainedEntry.PreviousHash"/>) are prepended by the sealer, never by this method, so a consumer cannot accidentally omit them.</remarks>
/// <typeparam name="TEntry">The consumer's entry type whose domain fields are hashed.</typeparam>
public interface IChainedEntryCanonicalizer<in TEntry>
{
    /// <summary>Gets the scheme version this canonicalizer writes — the verifier selects a canonicalizer by this value so a field-set change ships as a new version rather than a breaking re-hash of history.</summary>
    int SchemeVersion { get; }

    /// <summary>Appends the entry's domain fields to <paramref name="builder"/> in a fixed order — the SDK supplies the chain fields, the consumer supplies the rest.</summary>
    /// <param name="entry">The entry whose domain fields are being hashed.</param>
    /// <param name="builder">The unambiguous encoder to append fields to.</param>
    void Write(TEntry entry, ICanonicalPayloadBuilder builder);
}
