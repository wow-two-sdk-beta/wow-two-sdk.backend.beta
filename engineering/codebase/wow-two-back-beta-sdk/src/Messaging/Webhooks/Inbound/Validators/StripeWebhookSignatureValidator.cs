using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Validates Stripe's signature: <c>Stripe-Signature: t=…,v1=…</c>, hex HMAC over <c>{t}.{body}</c>.</summary>
/// <remarks>The endpoint secret (<c>whsec_…</c>) keys the HMAC as written. The event id and type come from the payload.</remarks>
public sealed class StripeWebhookSignatureValidator : IWebhookSignatureValidator
{
    /// <inheritdoc />
    public WebhookSignatureResult Validate(IHeaderDictionary headers, ReadOnlySpan<byte> body, WebhookReceiverOptions receiver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(receiver);
        string? header = headers["Stripe-Signature"];
        if (string.IsNullOrEmpty(header))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Missing);

        var timestamps = header.ValuesOf(',', "t");
        var candidates = header.ValuesOf(',', "v1");
        if (timestamps.Count != 1 || candidates.Count == 0 || !timestamps[0].TryReadUnixSeconds(out var signedAt))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Malformed);

        var content = body.Around(timestamps[0] + ".");
        if (!receiver.Secrets.Any(secret => candidates.Any(secret.Utf8().HmacSha256(content).MatchesHex)))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Mismatch);
        if (!signedAt.IsWithin(now, receiver.Tolerance))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Stale);

        return new WebhookSignatureResult { DeliveryId = body.JsonProperty("id"), EventType = body.JsonProperty("type"), Timestamp = signedAt };
    }
}
