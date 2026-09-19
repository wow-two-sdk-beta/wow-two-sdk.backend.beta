using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Integrates the process environment as the master-key source — a base64-encoded 256-bit key read from one variable.</summary>
/// <remarks>The env-var name comes from <see cref="EnvelopeCryptographyOptions.MasterKeyEnvironmentVariable"/>. Suitable for containers and local development; swap for a KMS/HSM-backed broker in production by registering your own <see cref="IMasterKeyBroker"/> before <c>AddEnvelopeCryptography</c>. Never logs the key value.</remarks>
internal sealed class EnvironmentMasterKeyBroker(EnvelopeCryptographyOptions options) : IMasterKeyBroker
{
    private readonly EnvelopeCryptographyOptions _options = options;

    /// <inheritdoc />
    public byte[]? TryLoadKey()
    {
        var envVar = _options.MasterKeyEnvironmentVariable;
        var raw = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        byte[] key;
        try
        {
            key = Convert.FromBase64String(raw);
        }
        catch (FormatException ex)
        {
            // Never surface the raw value — only the env-var name, which is not secret.
            throw new MasterKeyFormatException($"Master key in '{envVar}' is not valid base64.", ex);
        }

        if (key.Length != KeySizeConstants.SymmetricKeyBytes)
        {
            // Clear the wrong-sized buffer before throwing — it may still hold partial key material.
            CryptographicOperations.ZeroMemory(key);
            throw new MasterKeyFormatException(
                $"Master key in '{envVar}' must decode to {KeySizeConstants.SymmetricKeyBytes} bytes.");
        }

        return key;
    }
}
