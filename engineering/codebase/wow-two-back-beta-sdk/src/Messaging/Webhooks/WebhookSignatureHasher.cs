using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Computes the webhook signature: <c>sha256=&lt;hex&gt;</c> over <c>timestamp + "." + body</c>, keyed by the subscription secret (built-in <see cref="HMACSHA256"/>).</summary>
/// <remarks>The scheme is chosen at registration, so a second one ships beside this rather than replacing it.</remarks>
public sealed class WebhookSignatureHasher : IWebhookSignatureHasher
{
    /// <summary>The signature scheme prefix (<c>sha256</c>).</summary>
    public const string Sha256Scheme = "sha256";

    /// <inheritdoc />
    public string Scheme => Sha256Scheme;

    /// <inheritdoc />
    /// <returns><c>sha256=&lt;lowercase-hex&gt;</c>.</returns>
    public string Create(string secret, string timestamp, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(timestamp);

        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var buffer = new byte[prefix.Length + payload.Length];
        prefix.CopyTo(buffer.AsSpan());
        payload.CopyTo(buffer.AsSpan(prefix.Length));

        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, buffer);
        return $"{Sha256Scheme}={Convert.ToHexStringLower(hash)}";
    }
}
