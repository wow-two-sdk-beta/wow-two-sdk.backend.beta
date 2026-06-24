namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Supplies the master key (KEK) material to an <see cref="ISealKeeper"/>, abstracting where the key comes from.</summary>
/// <remarks>The seam that makes the key source swappable — an environment variable today (<see cref="EnvironmentMasterKeyProvider"/>), a cloud KMS or HSM later — without touching the seal keeper or the crypto plane. Implementations should treat the returned bytes as sensitive and avoid caching them; the seal keeper holds the single in-memory copy.</remarks>
public interface IMasterKeyProvider
{
    /// <summary>Loads the raw master-key bytes, or <see langword="null"/> when no key is configured (the seal keeper then stays sealed rather than throwing).</summary>
    /// <returns>The 256-bit (32-byte) master key, or <see langword="null"/> when unavailable.</returns>
    /// <exception cref="MasterKeyFormatException">A key is configured but malformed — wrong encoding or wrong length.</exception>
    byte[]? TryLoadKey();
}
