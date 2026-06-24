namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>An AES-GCM authenticated-encryption blob — the nonce, authentication tag, and ciphertext that together let a holder of the key recover the plaintext and detect tampering.</summary>
/// <remarks>Self-describing: persist or transmit all three fields together — decryption needs every one. None of the fields is secret on its own; confidentiality lives entirely in the key.</remarks>
/// <param name="Nonce">The 96-bit (12-byte) nonce used for this encryption. Generated fresh per operation and never reused under the same key — reuse breaks GCM's confidentiality and integrity guarantees.</param>
/// <param name="Tag">The 128-bit (16-byte) GCM authentication tag. Verifies the ciphertext and associated data on decrypt; a mismatch throws rather than returning corrupt plaintext.</param>
/// <param name="CipherText">The encrypted bytes — same length as the original plaintext (GCM is a stream cipher mode, so no padding is added).</param>
public sealed record EncryptedPayload(byte[] Nonce, byte[] Tag, byte[] CipherText);
