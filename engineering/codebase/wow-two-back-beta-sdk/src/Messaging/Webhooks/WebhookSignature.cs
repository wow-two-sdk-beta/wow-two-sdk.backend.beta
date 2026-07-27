using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Well-known HTTP header keys set on every webhook delivery.</summary>
public static class WebhookHeaders
{
    /// <summary><c>X-Webhook-Signature</c> — <c>sha256=&lt;hex&gt;</c> HMAC of the signed content.</summary>
    public const string Signature = "X-Webhook-Signature";

    /// <summary><c>X-Webhook-Timestamp</c> — the unix-seconds timestamp fed into the signature.</summary>
    public const string Timestamp = "X-Webhook-Timestamp";

    /// <summary><c>X-Webhook-Event</c> — the delivered event type.</summary>
    public const string Event = "X-Webhook-Event";

    /// <summary><c>X-Webhook-Id</c> — per-delivery id, for receiver-side dedupe.</summary>
    public const string Id = "X-Webhook-Id";
}

/// <summary>Well-known defaults for webhook delivery.</summary>
public static class WebhookDefaults
{
    /// <summary>Name of the delivery <c>HttpClient</c> resolved from <c>IHttpClientFactory</c>. Add handlers/policies via <c>AddHttpClient(WebhookDefaults.HttpClientName)</c>.</summary>
    public const string HttpClientName = "webhooks";
}

/// <summary>Computes the webhook signature: <c>sha256=&lt;hex&gt;</c> over <c>timestamp + "." + body</c>, keyed by the subscription secret (built-in <see cref="HMACSHA256"/>).</summary>
public static class WebhookSignature
{
    /// <summary>The signature scheme prefix (<c>sha256</c>).</summary>
    public const string Scheme = "sha256";

    /// <summary>Compute the <see cref="WebhookHeaders.Signature"/> value for a payload.</summary>
    /// <param name="secret">The subscription's HMAC-SHA256 secret.</param>
    /// <param name="timestamp">The unix-seconds timestamp (also sent as <see cref="WebhookHeaders.Timestamp"/>).</param>
    /// <param name="payload">The request body bytes — signed verbatim.</param>
    /// <returns><c>sha256=&lt;lowercase-hex&gt;</c>.</returns>
    public static string Create(string secret, string timestamp, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(timestamp);

        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var buffer = new byte[prefix.Length + payload.Length];
        prefix.CopyTo(buffer.AsSpan());
        payload.CopyTo(buffer.AsSpan(prefix.Length));

        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, buffer);
        return $"{Scheme}={Convert.ToHexStringLower(hash)}";
    }
}
