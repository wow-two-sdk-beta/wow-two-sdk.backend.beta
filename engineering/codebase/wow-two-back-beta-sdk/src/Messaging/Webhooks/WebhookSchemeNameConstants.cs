namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Holds the names of the built-in signature schemes: inbound validators are keyed by them, outbound issuers carry them.</summary>
public static class WebhookSchemeNameConstants
{
    /// <summary>Holds <c>wow2</c> — this SDK's outbound scheme: <c>X-Webhook-Signature: sha256=…</c> over <c>{timestamp}.{body}</c>.</summary>
    public const string Wow2 = "wow2";

    /// <summary>Holds <c>standard</c> — Standard Webhooks (Svix): <c>webhook-signature: v1,…</c> over <c>{id}.{timestamp}.{body}</c>.</summary>
    public const string Standard = "standard";

    /// <summary>Holds <c>stripe</c> — <c>Stripe-Signature: t=…,v1=…</c> over <c>{t}.{body}</c>.</summary>
    public const string Stripe = "stripe";

    /// <summary>Holds <c>github</c> — <c>X-Hub-Signature-256: sha256=…</c> over the body.</summary>
    public const string GitHub = "github";

    /// <summary>Holds <c>shopify</c> — <c>X-Shopify-Hmac-Sha256</c>, base64, over the body.</summary>
    public const string Shopify = "shopify";

    /// <summary>Holds <c>slack</c> — <c>X-Slack-Signature: v0=…</c> over <c>v0:{timestamp}:{body}</c>.</summary>
    public const string Slack = "slack";

    /// <summary>Holds <c>paddle</c> — <c>Paddle-Signature: ts=…;h1=…</c> over <c>{ts}:{body}</c>.</summary>
    public const string Paddle = "paddle";

    /// <summary>Holds <c>telegram</c> — the bot API's <c>X-Telegram-Bot-Api-Secret-Token</c> header equals the secret.</summary>
    public const string Telegram = "telegram";
}
