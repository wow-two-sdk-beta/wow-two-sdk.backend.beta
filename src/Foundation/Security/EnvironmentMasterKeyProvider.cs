using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Default <see cref="IMasterKeyProvider"/> — reads a base64-encoded 256-bit master key from an environment variable.</summary>
/// <remarks>The env-var name comes from <see cref="EnvelopeCryptographyOptions.MasterKeyEnvironmentVariable"/>. Suitable for containers and local development; swap for a KMS/HSM-backed provider in production by registering your own <see cref="IMasterKeyProvider"/> before <c>AddEnvelopeCryptography</c>. Never logs the key value.</remarks>
internal sealed class EnvironmentMasterKeyProvider(IOptions<EnvelopeCryptographyOptions> options) : IMasterKeyProvider
{
    private readonly EnvelopeCryptographyOptions _options = options.Value;

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

        if (key.Length != KeySizes.SymmetricKeyBytes)
        {
            // Clear the wrong-sized buffer before throwing — it may still hold partial key material.
            CryptographicOperations.ZeroMemory(key);
            throw new MasterKeyFormatException(
                $"Master key in '{envVar}' must decode to {KeySizes.SymmetricKeyBytes} bytes.");
        }

        return key;
    }
}
