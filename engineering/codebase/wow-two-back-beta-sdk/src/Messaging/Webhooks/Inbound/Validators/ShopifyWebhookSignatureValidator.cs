using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Validates Shopify's signature: <c>X-Shopify-Hmac-Sha256</c>, base64 HMAC over the body.</summary>
/// <remarks>
/// Shopify retries for up to 48 hours with the original <c>X-Shopify-Triggered-At</c>, so that time is reported but not
/// checked; replays are stopped by deduplicating <c>X-Shopify-Webhook-Id</c>.
/// </remarks>
public sealed class ShopifyWebhookSignatureValidator : IWebhookSignatureValidator
{
    /// <inheritdoc />
    public WebhookSignatureResult Validate(IHeaderDictionary headers, ReadOnlySpan<byte> body, WebhookReceiverOptions receiver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(receiver);
        string? signature = headers["X-Shopify-Hmac-Sha256"];
        if (string.IsNullOrEmpty(signature))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Missing);

        var content = body.ToArray();
        if (!receiver.Secrets.Any(secret => secret.Utf8().HmacSha256(content).MatchesBase64(signature)))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Mismatch);

        return new WebhookSignatureResult
        {
            DeliveryId = headers["X-Shopify-Webhook-Id"],
            EventType = headers["X-Shopify-Topic"],
            Timestamp = DateTimeOffset.TryParse(headers["X-Shopify-Triggered-At"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var triggered) ? triggered : null,
        };
    }
}
