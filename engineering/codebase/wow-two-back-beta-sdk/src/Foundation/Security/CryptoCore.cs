namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Default <see cref="ICryptoCore"/> — envelope-encryption data plane built on AES-256-GCM.</summary>
/// <remarks>A thin, stateless adapter over <see cref="AesGcmCipher"/>; safe to register as a singleton and share across threads.</remarks>
internal sealed class CryptoCore : ICryptoCore
{
    private readonly AesGcmCipher _cipher = new();

    /// <inheritdoc />
    public EncryptedPayload Encrypt(byte[] plaintext, byte[] dataKey, string associatedData) =>
        _cipher.Encrypt(dataKey, plaintext, associatedData);

    /// <inheritdoc />
    public byte[] Decrypt(EncryptedPayload payload, byte[] dataKey, string associatedData) =>
        _cipher.Decrypt(dataKey, payload, associatedData);
}
