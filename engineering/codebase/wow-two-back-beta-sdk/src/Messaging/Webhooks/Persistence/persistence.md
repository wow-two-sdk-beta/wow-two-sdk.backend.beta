# Messaging.Webhooks.Persistence

Durable outbound webhooks over Entity Framework Core: subscriptions in `webhook_subscriptions`, finished deliveries in
`webhook_deliveries`, and redelivery of a recorded delivery with its original id.

## Quick start

```csharp
// OnModelCreating
modelBuilder.ApplyWebhookSchema();

builder.Services.AddWebhooks();
builder.Services.AddWebhookEntityFrameworkStores<AppDbContext>();     // Webhooks:Persistence

await subscriptions.AddAsync(new WebhookSubscription { Id = "acme", Url = new("https://acme.test/hooks"), Secret = secret }, ct);
await subscriptions.AddAsync(stored with { Enabled = false }, ct);    // keep it, stop deliveries
var history = await deliveries.ListAsync("acme", limit: 50, ct);     // newest first
await redelivery.RedeliverAsync(history[0].DeliveryId, ct);          // same body, same X-Webhook-Id, fresh signature
await deliveries.PurgeAsync(time.GetUtcNow().AddDays(-30), ct);      // from a daily job
```

## Notes

- Secrets are stored through ASP.NET Data Protection (`dp1:` prefix); persist and share the key ring across hosts.
  A secret that no longer unprotects skips its subscription with an error log, and deliveries continue for the rest.
- `ProtectSecrets: false` stores secrets as given; rows written either way stay readable after a switch.
- Subscriptions seeded in `WebhookOptions.Subscriptions` are listed beside stored ones; a stored id wins.
- Bodies over `MaxStoredPayloadBytes` (256 KiB) are logged without the body, so they cannot be redelivered.
- A failed log write is logged as a warning and never fails the publish.
- Each repository call runs in its own DI scope, so the singleton publisher uses a scoped `DbContext` safely.
- Existing databases need a migration for the two tables.
