# Inbound event ordering

> Provider webhooks arrive twice and out of order; apply each event once and never let an older one win.

```csharp
// Registration: the EF inbox shares the product context, so the dedupe mark commits with the effect.
builder.Services.AddEfInbox<AppDbContext>();          // and modelBuilder.ApplyInboxModel() + an inbox_messages migration

// Handler: dedupe by the provider's event id, then advance by the provider's event time.
bool applied = await inbox.ProcessOnceAsync($"stripe:{stripeEvent.Id}", async ct =>
{
    await db.Subscriptions
        .Where(subscription => subscription.StripeSubscriptionId == subscriptionId)
        .ExecuteUpdateIfNewerAsync(
            subscription => subscription.LastEventAt,
            stripeEvent.Created,
            setters => setters.SetProperty(subscription => subscription.Status, status),
            ct);
}, ct);
```

- `IInboxProcessor.ProcessOnceAsync` records the event id and the effect in one transaction; a redelivery returns `false`.
- `ExecuteUpdateIfNewerAsync` updates only where the watermark is null or older, in one statement.
- An equal position counts as stale, so a replay that slipped past the inbox changes nothing.
- A skipped stale event is not an error: acknowledge it so the provider stops retrying.
- Entitlement and grace-period policy stay in the product; this only orders state.
