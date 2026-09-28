using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Validates the Standard Webhooks signature, which Svix-powered senders also use under <c>svix-*</c> headers.</summary>
/// <remarks>
/// <c>webhook-signature</c> lists space-separated <c>v1,&lt;base64&gt;</c> entries over <c>{webhook-id}.{webhook-timestamp}.{body}</c>;
/// the key is the base64 after <c>whsec_</c>. The event type comes from the payload's <c>type</c>.
/// </remarks>
public sealed class StandardWebhookSignatureValidator : IWebhookSignatureValidator
{
    private const string SecretPrefix = "whsec_";
    private const string VersionPrefix = "v1,";

    /// <inheritdoc />
    public WebhookSignatureResult Validate(IHeaderDictionary headers, ReadOnlySpan<byte> body, WebhookReceiverOptions receiver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(receiver);
        var id = Header(headers, "id");
        var timestamp = Header(headers, "timestamp");
        var signatures = Header(headers, "signature");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signatures))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Missing);
        if (!timestamp.TryReadUnixSeconds(out var signedAt))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Malformed);

        var candidates = signatures.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => entry.StartsWith(VersionPrefix, StringComparison.Ordinal))
            .Select(entry => entry[VersionPrefix.Length..])
            .ToList();
        if (candidates.Count == 0)
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Malformed);

        var content = body.Around($"{id}.{timestamp}.");
        if (!receiver.Secrets.Select(KeyOf).Any(key => candidates.Any(key.HmacSha256(content).MatchesBase64)))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Mismatch);
        if (!signedAt.IsWithin(now, receiver.Tolerance))
            return WebhookSignatureResult.Failed(WebhookSignatureFailure.Stale);

        return new WebhookSignatureResult { DeliveryId = id, EventType = body.JsonProperty("type"), Timestamp = signedAt };
    }

    /// <summary>The <c>webhook-*</c> header, else its <c>svix-*</c> twin.</summary>
    private static string? Header(IHeaderDictionary headers, string name)
    {
        string? standard = headers["webhook-" + name];
        return string.IsNullOrEmpty(standard) ? headers["svix-" + name] : standard;
    }

    /// <summary>The base64 key after <c>whsec_</c>; a secret that is not base64 signs with its UTF-8 bytes.</summary>
    private static byte[] KeyOf(string secret)
    {
        var encoded = secret.StartsWith(SecretPrefix, StringComparison.Ordinal) ? secret[SecretPrefix.Length..] : secret;
        var key = new byte[encoded.Length];
        return Convert.TryFromBase64String(encoded, key, out var written) ? key[..written] : secret.Utf8();
    }
}
