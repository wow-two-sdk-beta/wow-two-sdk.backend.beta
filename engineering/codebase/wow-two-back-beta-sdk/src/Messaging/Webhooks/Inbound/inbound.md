# Messaging.Webhooks.Inbound

Receiving webhooks: validate the sender's signature over the raw body, reject replays, and run the handler once per
delivery id. Eight provider schemes ship; a product adds one by registering a keyed `IWebhookSignatureValidator`.

## Quick start

```csharp
builder.Services.AddInboundWebhooks();

app.MapPost("/webhooks/stripe", async (WebhookReceiptModel webhook, IBillingService billing) =>
{
    var evt = webhook.ReadJson<StripeEvent>()!;          // the verified raw body
    await billing.ApplyAsync(evt, webhook.DeliveryId);
    return Results.Ok();
}).RequireWebhookSignature("stripe");
```

```jsonc
"Webhooks": { "Inbound": { "Receivers": {
  "stripe": { "Secrets": [ "whsec_…" ] },                          // scheme defaults to the receiver name
  "billing": { "Scheme": "paddle", "Secrets": [ "pdl_ntfset_…" ], "Tolerance": "00:05:00" }
} } }
```

| Scheme | Signature | Delivery id | Replay window |
|---|---|---|---|
| `wow2` | `X-Webhook-Signature: sha256=…` over `{timestamp}.{body}` — this SDK's outbound | `X-Webhook-Id` | timestamp |
| `standard` | Standard Webhooks / Svix `v1,<base64>` over `{id}.{timestamp}.{body}` | `webhook-id` | timestamp |
| `stripe` | `Stripe-Signature: t=…,v1=…` | payload `id` | timestamp |
| `github` | `X-Hub-Signature-256: sha256=…` over the body | `X-GitHub-Delivery` | dedupe only |
| `shopify` | `X-Shopify-Hmac-Sha256` base64 over the body | `X-Shopify-Webhook-Id` | dedupe only |
| `slack` | `X-Slack-Signature: v0=…` over `v0:{timestamp}:{body}` | payload `event_id` | timestamp |
| `paddle` | `Paddle-Signature: ts=…;h1=…` over `{ts}:{body}` | payload `event_id` | timestamp |
| `telegram` | `X-Telegram-Bot-Api-Secret-Token` equals the secret | payload `update_id` | dedupe only |

## Notes

- An unverified delivery answers `401` problem details (`messageKey` `WebhookSignatureInvalid`); an oversized one `413`.
- Several secrets validate side by side, so a rotation lists the new secret beside the old one.
- Deduplication stores handled ids in the registered `IIdempotencyRepository` (in-memory by default; use the SQL or
  Redis repository across hosts). Only a success is remembered; a repeat racing the first answers `409`.
- Read the payload from the receipt: binding the body separately consumes the bytes the signature covers.
- `WebhookReceiverService.ReceiveAsync(context, "name")` validates outside minimal APIs, such as in a controller.
- Custom scheme: `services.AddKeyedSingleton<IWebhookSignatureValidator, AcmeValidator>("acme")`; keys are lowercase.
