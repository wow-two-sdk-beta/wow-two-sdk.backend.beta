namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Encrypts and decrypts arbitrary values under a caller-supplied data-encryption key (DEK) — the data-plane half of envelope encryption.</summary>
/// <remarks>The key is passed in per call rather than held: this type never sees the master key and stays oblivious to where the DEK came from, so the same instance serves every owner and tenant. Pair with an <see cref="ISealKeeper"/>, which wraps and unwraps the DEK under a master key.</remarks>
public interface ICryptoCore
{
    /// <summary>Encrypts <paramref name="plaintext"/> under <paramref name="dataKey"/>, binding it to <paramref name="associatedData"/> so the same context must be supplied to decrypt.</summary>
    /// <param name="plaintext">The bytes to protect.</param>
    /// <param name="dataKey">The 256-bit data-encryption key (typically unwrapped via <see cref="ISealKeeper.UnwrapDataKey"/>).</param>
    /// <param name="associatedData">Non-secret context authenticated alongside the ciphertext — e.g. a logical record key.</param>
    EncryptedPayload Encrypt(byte[] plaintext, byte[] dataKey, string associatedData);

    /// <summary>Decrypts <paramref name="payload"/> under <paramref name="dataKey"/>, verifying the tag against <paramref name="associatedData"/>.</summary>
    /// <param name="payload">The encrypted blob produced by <see cref="Encrypt"/>.</param>
    /// <param name="dataKey">The same data-encryption key used to encrypt.</param>
    /// <param name="associatedData">The exact context string used to encrypt; a mismatch fails authentication.</param>
    /// <exception cref="System.Security.Cryptography.AuthenticationTagMismatchException">The payload, key, or associated data does not verify.</exception>
    byte[] Decrypt(EncryptedPayload payload, byte[] dataKey, string associatedData);
}
