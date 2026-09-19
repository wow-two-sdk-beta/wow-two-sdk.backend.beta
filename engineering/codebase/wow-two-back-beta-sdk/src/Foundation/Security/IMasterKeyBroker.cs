namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Defines behavior that integrates the master-key (KEK) source an <see cref="ISealService"/> unseals from, so the source swaps without touching the crypto plane.</summary>
/// <remarks>An environment variable today (<see cref="EnvironmentMasterKeyBroker"/>), a cloud KMS or HSM later. Implementations should treat the returned bytes as sensitive and avoid caching them; the seal service holds the single in-memory copy.</remarks>
public interface IMasterKeyBroker
{
    /// <summary>Loads the raw master-key bytes, or <see langword="null"/> when no key is configured (the seal keeper then stays sealed rather than throwing).</summary>
    /// <returns>The 256-bit (32-byte) master key, or <see langword="null"/> when unavailable.</returns>
    /// <exception cref="MasterKeyFormatException">A key is configured but malformed — wrong encoding or wrong length.</exception>
    byte[]? TryLoadKey();
}
