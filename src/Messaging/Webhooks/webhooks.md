# Messaging.Webhooks

Outbound webhook delivery — fan an event out to subscribed HTTP endpoints, each POST **HMAC-SHA256 signed** so the
receiver can verify authenticity. A thin outbound sink beside the event bus: the same `IEvent` a handler publishes
in-process can be mirrored to external subscribers. The in-memory subscription store is the zero-config default;
delivery rides `IHttpClientFactory` with a bounded retry budget on transient failures (5xx / 408 / timeout), reusing the
messaging `IRetryPolicy`.

> **Outbound only.** No inbound receiver, no management API, no persistence — subscriptions live in memory. Delivery is
> at-least-once per subscription within a bounded retry budget; on exhaustion the attempt is logged and dropped.

## Layout

| File | What |
|---|---|
| `WebhookContracts.cs` | `IWebhookPublisher`, `WebhookSubscription`, `IWebhookSubscriptionStore`, `IWebhookDeliveryLog` + `WebhookDeliveryRecord`/`WebhookDeliveryOutcome`, `WebhookHeaders`, `WebhookDefaults`, `WebhookSignature` |
| `WebhookOptions.cs` | Subscriptions seed + retry / timeout knobs (mutable, `Action<T>`-friendly) |
| `InMemoryWebhookSubscriptionStore.cs` | Default store — seeded from options, thread-safe, runtime `AddAsync` |
| `WebhookPublisher.cs` | `WebhookPublisher` (resolve → fan-out) + `HttpWebhookDispatcher` (sign → POST → bounded retry) + no-op delivery log |
| `WebhooksServiceCollectionExtensions.cs` | `AddWebhooks(...)` |

## Quick start

```csharp
builder.Services.AddWebhooks(o =>
{
    o.Subscriptions.Add(new WebhookSubscription
    {
        Url             = new Uri("https://partner.example/hooks"),
        Secret          = "whsec_…",     // HMAC key
        EventTypeFilter = "order.*",     // glob: * (any run) and ? (one char)
    });
});

// from a handler — typed overload serializes the event to a JSON body; event type = CLR type name:
public sealed class MirrorOrderHandler(IWebhookPublisher webhooks) : IEventHandler<OrderPlaced>
{
    public ValueTask HandleAsync(EventContext<OrderPlaced> ctx, CancellationToken ct)
        => webhooks.PublishAsync(ctx.Event, ct);
}

// or raw — you own the event type string + payload bytes:
await webhooks.PublishAsync("order.created", JsonSerializer.SerializeToUtf8Bytes(payload), ct);
```

Each matching subscription receives a `POST` carrying:

| Header | Value |
|---|---|
| `X-Webhook-Signature` | `sha256=<hex>` — HMAC-SHA256 over `timestamp + "." + body`, keyed by the subscription secret |
| `X-Webhook-Timestamp` | unix seconds — also part of the signed string, so the receiver can reject stale replays |
| `X-Webhook-Event` | the event type |
| `X-Webhook-Id` | per-delivery id, for receiver-side dedupe |

Receiver verifies: recompute `HMAC(secret, "{X-Webhook-Timestamp}.{rawBody}")`, constant-time compare to the header.

## Delivery & retry

- **Fan-out**: every subscription whose `EventTypeFilter` glob matches the event type gets its own signed POST.
- **Transient** failure (HTTP 5xx / 408 / request timeout / connection error) → retried per `IRetryPolicy`
  (`MaxAttempts`, exponential-jitter backoff).
- **Permanent** failure (any other 4xx) → not retried.
- On success or terminal drop, a `WebhookDeliveryRecord` is handed to `IWebhookDeliveryLog` (no-op default).

## Security (SSRF guard)

Outbound webhooks POST to caller-supplied URLs — an SSRF vector — so delivery is guarded by default:

- **HTTPS required** (`RequireHttps`, default on) — an `http://` target is rejected without a send.
- **Private-address block** (`AllowPrivateNetworkTargets`, default off) — targets resolving to private / loopback /
  link-local / ULA / CGNAT / multicast (incl. cloud metadata `169.254.169.254`) are blocked at **connect time** on the
  actual resolved IP, so DNS-rebinding is defeated too. Enable only for trusted internal targets.
- **Host allowlist** (`AllowedHosts`, optional) — when non-empty, only listed hosts may receive deliveries.
- A blocked target is a permanent drop (no retry) — logged and recorded to `IWebhookDeliveryLog`.

## See also

- [Webhooks.spec.md](./Webhooks.spec.md) · [Webhooks.standard.md](./Webhooks.standard.md)
- Retry primitive: [`../Reliability/MessagingReliability.cs`](../Reliability/MessagingReliability.cs) (`IRetryPolicy` / `RetryConfig`)
- Bus contracts: [`../MessagingContracts.cs`](../MessagingContracts.cs) · [`../Messaging.md`](../Messaging.md)
