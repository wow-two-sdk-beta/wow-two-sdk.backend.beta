using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Issuers;

/// <summary>
/// Issues Standard Webhooks headers — <c>webhook-id</c>, <c>webhook-timestamp</c> and <c>webhook-signature: v1,…</c>
/// over <c>{id}.{timestamp}.{body}</c> — which receivers verify with any Standard Webhooks or Svix library.
/// </summary>
/// <remarks>Give subscriptions <c>whsec_</c> base64 secrets of 24 to 64 bytes, as the specification asks.</remarks>
public sealed class StandardWebhookSignatureIssuer : IWebhookSignatureIssuer
{
    /// <inheritdoc />
    public string Scheme => WebhookSchemeNameConstants.Standard;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Issue(WebhookSigningModel signing)
    {
        ArgumentNullException.ThrowIfNull(signing);
        var timestamp = signing.Timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var prefix = Encoding.UTF8.GetBytes($"{signing.DeliveryId}.{timestamp}.");
        var content = new byte[prefix.Length + signing.Payload.Length];
        prefix.CopyTo(content, 0);
        signing.Payload.Span.CopyTo(content.AsSpan(prefix.Length));
        var signature = Convert.ToBase64String(HMACSHA256.HashData(StandardWebhookKeyMapper.KeyOf(signing.Secret), content));
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["webhook-id"] = signing.DeliveryId,
            ["webhook-timestamp"] = timestamp,
            ["webhook-signature"] = "v1," + signature,
        };
    }
}
