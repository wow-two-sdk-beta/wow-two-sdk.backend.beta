using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Issuers;

/// <summary>Issues this SDK's signature headers: <c>X-Webhook-Signature</c> from the registered hasher, timestamp and id.</summary>
/// <param name="hasher">Computes the <c>sha256=</c> signature.</param>
public sealed class Wow2WebhookSignatureIssuer(IWebhookSignatureHasher hasher) : IWebhookSignatureIssuer
{
    /// <inheritdoc />
    public string Scheme => WebhookSchemeNameConstants.Wow2;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Issue(WebhookSigningModel signing)
    {
        ArgumentNullException.ThrowIfNull(signing);
        var timestamp = signing.Timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WebhookHeaderConstants.Signature] = hasher.Create(signing.Secret, timestamp, signing.Payload.Span),
            [WebhookHeaderConstants.Timestamp] = timestamp,
            [WebhookHeaderConstants.Id] = signing.DeliveryId,
        };
    }
}
