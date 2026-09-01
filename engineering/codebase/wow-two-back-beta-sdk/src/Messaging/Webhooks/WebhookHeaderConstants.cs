using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Well-known HTTP header keys set on every webhook delivery.</summary>
public static class WebhookHeaderConstants
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
