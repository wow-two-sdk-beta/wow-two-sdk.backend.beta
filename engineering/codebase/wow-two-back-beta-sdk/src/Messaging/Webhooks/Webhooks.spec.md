# Messaging.Webhooks — spec

*Last updated: 2026-07-09*

> **Concrete API + usage.** Updated when the public surface changes.

## NuGet

```
WoW.Two.Sdk.Backend.Beta  (mono-lib; namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks)
```

## Quick start

```csharp
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWebhooks(o =>
{
    o.Subscriptions.Add(new WebhookSubscription
    {
        Url             = new Uri("https://partner.example/hooks"),
        Secret          = "whsec_live_123",
        EventTypeFilter = "order.*",
    });
    o.MaxAttempts    = 4;                       // total sends before drop
    o.RequestTimeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();
var webhooks = app.Services.GetRequiredService<IWebhookPublisher>();

await webhooks.PublishAsync("order.created", """{"id":42}"""u8.ToArray());
```

## Public API

### `WebhooksServiceCollectionExtensions`

| Method | Returns | Notes |
|---|---|---|
| `AddWebhooks(this IServiceCollection, Action<WebhookOptions>? configure = null)` | `IServiceCollection` | Registers the publisher, in-memory store, no-op delivery log, retry policy, and the named `HttpClient`. Idempotent (`TryAdd`). |

### `IWebhookPublisher` (`…Messaging.Webhooks`)

| Member | Notes |
|---|---|
| `ValueTask PublishAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken = default)` | Raw — resolve matching subscriptions, sign, deliver. `payload` is sent verbatim as the request body. |
| `ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken = default) where TEvent : class, IEvent` | Typed — `eventType = typeof(TEvent).Name`, body = `JsonSerializer.SerializeToUtf8Bytes(@event)`. |

### `WebhookSubscription` (`sealed record`)

| Property | Type | Default | Notes |
|---|---|---|---|
| `Id` | `string` | new GUID (`N`) | Stable id; used by the store + delivery log. |
| `Url` | `Uri` | *required* | Delivery endpoint (POST target). |
| `Secret` | `string` | *required* | HMAC-SHA256 key. |
| `EventTypeFilter` | `string` | `"*"` | Glob: `*` = any run, `?` = one char; case-insensitive. |
| `bool Matches(string eventType)` | method | — | True when the filter glob matches. |

### Stores & log

| Type | Notes |
|---|---|
| `IWebhookSubscriptionStore` | `GetMatchingAsync(eventType, ct) → IReadOnlyList<WebhookSubscription>` · `AddAsync(sub, ct)`. |
| `InMemoryWebhookSubscriptionStore` | Default — seeded from `WebhookOptions.Subscriptions`, thread-safe, supports runtime `AddAsync`. |
| `IWebhookDeliveryLog` | `RecordAsync(WebhookDeliveryRecord, ct)` — terminal-outcome seam. Default = no-op. |
| `WebhookDeliveryRecord` | `record`: `SubscriptionId`, `EventType`, `Url`, `Outcome`, `Attempts`, `StatusCode?`, `OccurredAtUtc`. |
| `WebhookDeliveryOutcome` | `Delivered` · `Dropped`. |

### `WebhookOptions` (mutable — `Action<T>`-friendly)

| Property | Type | Default | Notes |
|---|---|---|---|
| `Subscriptions` | `IList<WebhookSubscription>` | empty | Seed set copied into the store at startup. |
| `MaxAttempts` | `int` | `3` | Total delivery attempts per subscription before drop. |
| `BaseRetryDelay` | `TimeSpan` | `500ms` | Exp-jitter backoff base. |
| `MaxRetryDelay` | `TimeSpan` | `30s` | Backoff ceiling. |
| `RequestTimeout` | `TimeSpan` | `30s` | Per-attempt HTTP timeout (a timeout counts as transient). |

### Signing (`WebhookSignature`, `WebhookHeaders`, `WebhookDefaults`)

| Member | Notes |
|---|---|
| `WebhookSignature.Scheme` | `"sha256"`. |
| `WebhookSignature.Create(string secret, string timestamp, ReadOnlySpan<byte> payload)` | Returns `"sha256=<lowercase-hex>"` — `HMACSHA256(secret, UTF8(timestamp + ".") + payload)`. |
| `WebhookHeaders.Signature / Timestamp / Event / Id` | `X-Webhook-Signature` · `X-Webhook-Timestamp` · `X-Webhook-Event` · `X-Webhook-Id`. |
| `WebhookDefaults.HttpClientName` | `"webhooks"` — the named `HttpClient`; add delegating handlers / Polly to it via `AddHttpClient(WebhookDefaults.HttpClientName)`. |

## Signing scheme

```
timestamp       = unix-seconds (UTC) at send time
signed_content  = UTF8(timestamp) + "." + rawBody          // byte concat, "." is 0x2E
signature       = "sha256=" + lower-hex( HMACSHA256(secret, signed_content) )
```

Sent as `X-Webhook-Signature`; `timestamp` echoed in `X-Webhook-Timestamp`. Receiver-side verify:

```csharp
var expected = WebhookSignature.Create(secret, timestamp, rawBodyBytes);
var ok = CryptographicOperations.FixedTimeEquals(
    Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(receivedSignatureHeader));
// plus: reject if |now - timestamp| exceeds your tolerance window
```

## Usage examples

### Example A — mirror a bus event to subscribers

```csharp
public sealed class MirrorOrderHandler(IWebhookPublisher webhooks) : IEventHandler<OrderPlaced>
{
    public ValueTask HandleAsync(EventContext<OrderPlaced> ctx, CancellationToken ct)
        => webhooks.PublishAsync(ctx.Event, ct);
}
```

### Example B — register a subscription at runtime

```csharp
var store = app.Services.GetRequiredService<IWebhookSubscriptionStore>();
await store.AddAsync(new WebhookSubscription
{
    Url = new Uri("https://tenant-42.example/hooks"), Secret = secret, EventTypeFilter = "invoice.*",
});
```

### Example C — observe deliveries

```csharp
public sealed class MetricsDeliveryLog(IMeterFactory meters) : IWebhookDeliveryLog
{
    public ValueTask RecordAsync(WebhookDeliveryRecord record, CancellationToken ct)
    {
        // count by record.Outcome / record.StatusCode …
        return ValueTask.CompletedTask;
    }
}

// override the no-op default:
builder.Services.AddWebhooks(/* … */);
builder.Services.AddSingleton<IWebhookDeliveryLog, MetricsDeliveryLog>();
```

## Future

- **Configuration binding** — an `AddWebhooks(IConfiguration, section)` overload (subscriptions from `appsettings`).
- **Durable store** — an EF-backed `IWebhookSubscriptionStore` + management API (currently in-memory only).
- **Persistent delivery + dedupe** — outbox-staged deliveries, receiver-idempotency guarantees, DLQ for exhausted drops.
- **Concurrency & ordering** — deliveries fan out sequentially today; parallelism / per-subscription ordering unspecified.
- **Circuit-breaking a failing endpoint** — auto-disable a subscription after sustained failures.

## See also

- Standard: [Webhooks.standard.md](./Webhooks.standard.md) · folder: [webhooks.md](./webhooks.md)
- Retry primitive: [`../Reliability/MessagingReliability.cs`](../Reliability/MessagingReliability.cs)
- Built-ins: [`System.Security.Cryptography.HMACSHA256`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.hmacsha256) · [`IHttpClientFactory`](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory)
