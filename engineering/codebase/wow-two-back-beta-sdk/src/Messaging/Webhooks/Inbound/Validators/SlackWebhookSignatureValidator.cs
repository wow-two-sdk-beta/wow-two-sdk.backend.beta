using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Validates Slack's request signature: <c>X-Slack-Signature: v0=&lt;hex&gt;</c> over <c>v0:{timestamp}:{body}</c>.</summary>
/// <remarks>The signing secret keys the HMAC; Events API deliveries carry <c>event_id</c>, used to drop Slack's retries.</remarks>
public sealed class SlackWebhookSignatureValidator : IWebhookSignatureValidator
{
    private const string Prefix = "v0=";

    /// <inheritdoc />
    public WebhookSignatureResult Validate(IHeaderDictionary headers, ReadOnlySpan<byte> body, WebhookReceiverOptions receiver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(receiver);
        string? signature = headers["X-Slack-Signature"];
        string? timestamp = headers["X-Slack-Request-Timestamp"];
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Missing);
        if (!signature.StartsWith(Prefix, StringComparison.Ordinal) || !timestamp.TryReadUnixSeconds(out var signedAt))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Malformed);

        var content = body.Around($"v0:{timestamp}:");
        if (!receiver.Secrets.Any(secret => secret.Utf8().HmacSha256(content).MatchesHex(signature[Prefix.Length..])))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Mismatch);
        if (!signedAt.IsWithin(now, receiver.Tolerance))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Stale);

        return new WebhookSignatureResult { DeliveryId = body.JsonProperty("event_id"), EventType = body.JsonProperty("type"), Timestamp = signedAt };
    }
}
