using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Validates GitHub's signature: <c>X-Hub-Signature-256: sha256=&lt;hex&gt;</c> over the body.</summary>
/// <remarks>GitHub signs no timestamp, so replays are stopped by deduplicating <c>X-GitHub-Delivery</c>.</remarks>
public sealed class GitHubWebhookSignatureValidator : IWebhookSignatureValidator
{
    private const string Prefix = "sha256=";

    /// <inheritdoc />
    public WebhookSignatureResult Validate(IHeaderDictionary headers, ReadOnlySpan<byte> body, WebhookReceiverOptions receiver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(receiver);
        string? signature = headers["X-Hub-Signature-256"];
        if (string.IsNullOrEmpty(signature))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Missing);
        if (!signature.StartsWith(Prefix, StringComparison.Ordinal))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Malformed);

        var content = body.ToArray();
        if (!receiver.Secrets.Any(secret => secret.Utf8().HmacSha256(content).MatchesHex(signature[Prefix.Length..])))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Mismatch);

        return new WebhookSignatureResult { DeliveryId = headers["X-GitHub-Delivery"], EventType = headers["X-GitHub-Event"] };
    }
}
