using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>AES-256-GCM authenticated-encryption primitive — the single low-level building block every other type in this module composes.</summary>
/// <remarks>
/// AES-GCM is an AEAD (Authenticated Encryption with Associated Data) cipher — one call gives both confidentiality (the ciphertext) and integrity (the tag), and binds in a non-secret <c>associatedData</c> string that is authenticated but not encrypted. A fresh random 96-bit nonce is generated per <see cref="Encrypt"/> call (the GCM-recommended size); never reuse a nonce under the same key. Stateless and thread-safe — every operation constructs its own <see cref="AesGcm"/>.
/// </remarks>
internal static class AesGcmCipher
{
    /// <summary>96-bit nonce — the size AES-GCM is designed and standardized for; using it lets the runtime generate the nonce directly rather than re-hashing.</summary>
    private const int NonceSize = 12;

    /// <summary>128-bit authentication tag — the maximum (and recommended) GCM tag length, giving the strongest tamper detection.</summary>
    private const int TagSize = 16;

    /// <summary>Encrypts <paramref name="plaintext"/> under <paramref name="key"/>, generating a fresh random nonce and authenticating <paramref name="associatedData"/> alongside the ciphertext.</summary>
    /// <param name="key">The 256-bit (32-byte) AES key. The caller owns its lifetime and is responsible for clearing it.</param>
    /// <param name="plaintext">The bytes to encrypt.</param>
    /// <param name="associatedData">Context bound into the tag but left unencrypted — e.g. a logical key or owner id. The same value must be supplied to <see cref="Decrypt"/> or authentication fails.</param>
    /// <returns>An <see cref="EncryptedPayload"/> carrying the nonce, tag, and ciphertext.</returns>
    public static EncryptedPayload Encrypt(byte[] key, byte[] plaintext, string associatedData)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(associatedData);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherText = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var aad = Encoding.UTF8.GetBytes(associatedData);

        using var gcm = new AesGcm(key, TagSize);
        gcm.Encrypt(nonce, plaintext, cipherText, tag, aad);

        return new EncryptedPayload(nonce, tag, cipherText);
    }

    /// <summary>Decrypts <paramref name="payload"/> under <paramref name="key"/>, verifying the tag against <paramref name="associatedData"/>.</summary>
    /// <param name="key">The 256-bit (32-byte) AES key used at encryption time.</param>
    /// <param name="payload">The nonce, tag, and ciphertext produced by <see cref="Encrypt"/>.</param>
    /// <param name="associatedData">The exact context string supplied at encryption time; any difference fails authentication.</param>
    /// <returns>The recovered plaintext.</returns>
    /// <exception cref="AuthenticationTagMismatchException">The tag, ciphertext, key, nonce, or associated data does not match what was encrypted — the data is corrupt, tampered with, or decrypted with the wrong key.</exception>
    public static byte[] Decrypt(byte[] key, EncryptedPayload payload, string associatedData)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(associatedData);

        var plaintext = new byte[payload.CipherText.Length];
        var aad = Encoding.UTF8.GetBytes(associatedData);

        using var gcm = new AesGcm(key, TagSize);
        gcm.Decrypt(payload.Nonce, payload.CipherText, payload.Tag, plaintext, aad);

        return plaintext;
    }
}
