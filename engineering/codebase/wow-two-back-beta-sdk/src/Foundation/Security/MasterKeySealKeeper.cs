using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Default <see cref="ISealKeeper"/> — keeps the master key (KEK) in memory and wraps/unwraps data keys with AES-256-GCM.</summary>
/// <remarks>
/// Flow: <see cref="Unseal"/> pulls the KEK from the injected <see cref="IMasterKeyProvider"/> and holds it in a single private buffer; wrap/unwrap then run AES-GCM under that key. The source of the key is abstracted, so the same keeper works with env vars, a KMS, or an HSM. The key is cleared from memory on <see cref="Dispose"/>; no key material is ever logged. Register as a singleton (the unsealed key must outlive a request).
/// </remarks>
internal sealed partial class MasterKeySealKeeper(IMasterKeyProvider keyProvider, ILogger<MasterKeySealKeeper> logger)
    : ISealKeeper, IDisposable
{
    private byte[]? _masterKey;

    /// <inheritdoc />
    public bool IsSealed => _masterKey is null;

    /// <inheritdoc />
    public void Unseal()
    {
        if (!IsSealed)
            return;

        var key = keyProvider.TryLoadKey();
        if (key is null)
        {
            LogNoMasterKey();
            return;
        }

        _masterKey = key;
        LogUnsealed();
    }

    /// <inheritdoc />
    public byte[] GenerateDataKey() => RandomNumberGenerator.GetBytes(KeySizes.SymmetricKeyBytes);

    /// <inheritdoc />
    public EncryptedPayload WrapDataKey(byte[] dataKey, string associatedData)
    {
        EnsureUnsealed();
        return AesGcmCipher.Encrypt(_masterKey!, dataKey, associatedData);
    }

    /// <inheritdoc />
    public byte[] UnwrapDataKey(EncryptedPayload wrapped, string associatedData)
    {
        EnsureUnsealed();
        return AesGcmCipher.Decrypt(_masterKey!, wrapped, associatedData);
    }

    /// <summary>Clears the in-memory master key — call on shutdown so key material does not linger in memory.</summary>
    public void Dispose()
    {
        if (_masterKey is not null)
        {
            CryptographicOperations.ZeroMemory(_masterKey);
            _masterKey = null;
        }
    }

    private void EnsureUnsealed()
    {
        if (IsSealed)
            throw new InvalidOperationException("Envelope cryptography is sealed — the master key is not loaded.");
    }

    [LoggerMessage(EventId = 7001, Level = LogLevel.Warning, Message = "No master key configured — envelope cryptography remains sealed.")]
    private partial void LogNoMasterKey();

    [LoggerMessage(EventId = 7002, Level = LogLevel.Information, Message = "Envelope cryptography unsealed.")]
    private partial void LogUnsealed();
}
