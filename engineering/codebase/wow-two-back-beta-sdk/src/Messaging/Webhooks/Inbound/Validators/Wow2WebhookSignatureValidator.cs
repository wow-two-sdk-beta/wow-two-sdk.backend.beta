using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Validates this SDK's outbound signature, so one product can receive another's webhooks.</summary>
/// <remarks><c>X-Webhook-Signature: sha256=&lt;hex&gt;</c> over <c>{X-Webhook-Timestamp}.{body}</c>; the id and event travel in their own headers.</remarks>
public sealed class Wow2WebhookSignatureValidator : IWebhookSignatureValidator
{
    private const string Prefix = WebhookSignatureHasher.Sha256Scheme + "=";

    /// <inheritdoc />
    public WebhookSignatureResult Validate(IHeaderDictionary headers, ReadOnlySpan<byte> body, WebhookReceiverOptions receiver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(receiver);
        string? signature = headers[WebhookHeaderConstants.Signature];
        string? timestamp = headers[WebhookHeaderConstants.Timestamp];
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Missing);
        if (!signature.StartsWith(Prefix, StringComparison.Ordinal) || !timestamp.TryReadUnixSeconds(out var signedAt))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Malformed);

        var content = body.Around(timestamp + ".");
        if (!receiver.Secrets.Any(secret => secret.Utf8().HmacSha256(content).MatchesHex(signature[Prefix.Length..])))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Mismatch);
        if (!signedAt.IsWithin(now, receiver.Tolerance))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Stale);

        return new WebhookSignatureResult
        {
            DeliveryId = headers[WebhookHeaderConstants.Id],
            EventType = headers[WebhookHeaderConstants.Event],
            Timestamp = signedAt,
        };
    }
}
