using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Validates Paddle Billing's signature: <c>Paddle-Signature: ts=…;h1=…</c>, hex HMAC over <c>{ts}:{body}</c>.</summary>
/// <remarks>The notification destination's secret key keys the HMAC; the event id and type come from the payload.</remarks>
public sealed class PaddleWebhookSignatureValidator : IWebhookSignatureValidator
{
    /// <inheritdoc />
    public WebhookSignatureResult Validate(IHeaderDictionary headers, ReadOnlySpan<byte> body, WebhookReceiverOptions receiver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(receiver);
        string? header = headers["Paddle-Signature"];
        if (string.IsNullOrEmpty(header))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Missing);

        var timestamps = header.ValuesOf(';', "ts");
        var candidates = header.ValuesOf(';', "h1");
        if (timestamps.Count != 1 || candidates.Count == 0 || !timestamps[0].TryReadUnixSeconds(out var signedAt))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Malformed);

        var content = body.Around(timestamps[0] + ":");
        if (!receiver.Secrets.Any(secret => candidates.Any(secret.Utf8().HmacSha256(content).MatchesHex)))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Mismatch);
        if (!signedAt.IsWithin(now, receiver.Tolerance))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Stale);

        return new WebhookSignatureResult { DeliveryId = body.JsonProperty("event_id"), EventType = body.JsonProperty("event_type"), Timestamp = signedAt };
    }
}
