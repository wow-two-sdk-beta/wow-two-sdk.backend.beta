namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Holds the master key (KEK) in memory and wraps/unwraps data keys (DEKs) — the key-management half of envelope encryption.</summary>
/// <remarks>
/// Two-tier key model: one long-lived master key (KEK) encrypts many short-lived data keys (DEKs); each DEK encrypts the actual values via <see cref="ICryptoCore"/>. The keeper never persists the KEK — it lives only in process memory after <see cref="Unseal"/>, so a restart starts sealed. Register as a singleton so the unsealed key survives across requests.
/// </remarks>
public interface ISealKeeper
{
    /// <summary>Gets a value indicating the master key is not loaded — no wrap/unwrap is possible until <see cref="Unseal"/> succeeds.</summary>
    bool IsSealed { get; }

    /// <summary>Loads the master key into memory from the configured <see cref="IMasterKeyProvider"/>; a no-op leaving the keeper sealed when no key is configured.</summary>
    /// <exception cref="MasterKeyFormatException">A key is configured but malformed.</exception>
    void Unseal();

    /// <summary>Generates a new random 256-bit data key (DEK). The caller owns its lifetime and should clear it after use.</summary>
    /// <returns>Fresh 32-byte key material.</returns>
    byte[] GenerateDataKey();

    /// <summary>Encrypts (wraps) a DEK under the master key, binding it to <paramref name="associatedData"/> for storage alongside the data it protects.</summary>
    /// <param name="dataKey">The plaintext data key to wrap.</param>
    /// <param name="associatedData">Non-secret context authenticated into the wrapped blob — e.g. the owner the DEK belongs to.</param>
    /// <exception cref="System.InvalidOperationException">The keeper is sealed.</exception>
    EncryptedPayload WrapDataKey(byte[] dataKey, string associatedData);

    /// <summary>Decrypts (unwraps) a previously wrapped DEK under the master key, verifying the tag against <paramref name="associatedData"/>.</summary>
    /// <param name="wrapped">The wrapped blob produced by <see cref="WrapDataKey"/>.</param>
    /// <param name="associatedData">The exact context string used at wrap time; a mismatch fails authentication.</param>
    /// <exception cref="System.InvalidOperationException">The keeper is sealed.</exception>
    /// <exception cref="System.Security.Cryptography.AuthenticationTagMismatchException">The wrapped key or associated data does not verify.</exception>
    byte[] UnwrapDataKey(EncryptedPayload wrapped, string associatedData);
}
