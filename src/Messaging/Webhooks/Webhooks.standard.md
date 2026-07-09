# Messaging.Webhooks — standard

*Last updated: 2026-07-09*

> Behavioral contract (RFC 2119: MUST / SHOULD / MAY). Applies to the abstraction and any delivery implementation.

## Purpose

Deliver an application event to externally-registered HTTP endpoints (subscriptions), each request cryptographically
signed so the receiver can verify origin and integrity. Outbound only; a thin sink beside the event bus.

## Registration

- The package **MUST** expose `AddWebhooks(this IServiceCollection, Action<WebhookOptions>? configure = null)` returning `IServiceCollection`.
- Registration **MUST** be idempotent — repeated `AddWebhooks` calls **MUST NOT** double-register services (`TryAdd*`).
- Registration **MUST** register a default `IWebhookPublisher`, `IWebhookSubscriptionStore`, `IWebhookDeliveryLog`, and an
  `IRetryPolicy`, and **MUST** register a named `HttpClient` (`WebhookDefaults.HttpClientName`) via `IHttpClientFactory`.
- A consumer **MUST** be able to replace any of those services by registering its own before/after `AddWebhooks`
  (the default `IWebhookDeliveryLog` is a no-op and **MUST** be overridable).
- `WebhookOptions` **MUST** use settable properties so `Action<WebhookOptions>` configuration composes.

## Subscriptions & matching

- A subscription **MUST** carry a delivery `Url`, an HMAC `Secret`, and an `EventTypeFilter`; it **MUST** expose a stable `Id`.
- `EventTypeFilter` **MUST** be treated as a glob where `*` matches any run of characters and `?` matches exactly one;
  matching **SHOULD** be case-insensitive. A filter of `*` **MUST** match every event type.
- `IWebhookSubscriptionStore.GetMatchingAsync(eventType)` **MUST** return exactly the subscriptions whose filter matches.
- The default store **MUST** be in-memory, **MUST** seed from `WebhookOptions.Subscriptions` at construction, and **MUST**
  be safe for concurrent `GetMatchingAsync` / `AddAsync`.

## Signing

- Every delivered request **MUST** carry `X-Webhook-Signature`, `X-Webhook-Timestamp`, and `X-Webhook-Event` headers, and
  **SHOULD** carry a per-delivery `X-Webhook-Id`.
- The signature **MUST** be `sha256=` + lowercase hex of `HMACSHA256(secret, UTF8(timestamp + ".") + body)`, using the
  subscription's own secret and the built-in `System.Security.Cryptography.HMACSHA256`.
- `X-Webhook-Timestamp` **MUST** be the same timestamp fed into the signature (unix seconds), so a receiver can bound replay.
- The body sent over the wire **MUST** be byte-identical to the bytes fed into the signature (sign-what-you-send).

## Delivery & retry

- `PublishAsync` **MUST** deliver to every matching subscription (fan-out); each subscription's failure **MUST NOT**
  prevent delivery to the others.
- A delivery **MUST** be retried on a **transient** failure — HTTP 5xx, HTTP 408, a request timeout, or a connection
  error — until the resolved retry budget (`WebhookOptions.MaxAttempts`, via `IRetryPolicy`) is exhausted.
- A delivery **MUST NOT** be retried on a **permanent** failure — any non-transient 4xx response.
- A 2xx response **MUST** be treated as success.
- Retry backoff **MUST** follow the resolved `IRetryPolicy` (`NextDelay` returning `null` ends the budget).
- On retry-budget exhaustion the delivery **MUST** be logged (`ILogger`) and dropped; it **MUST NOT** throw out of `PublishAsync`.
- `OperationCanceledException` from the caller's `CancellationToken` **MUST** propagate and **MUST NOT** be retried,
  logged as a failure, or recorded as a drop.
- Each terminal outcome (delivered or dropped) **MUST** be reported to `IWebhookDeliveryLog` with the subscription id,
  event type, attempt count, and last status code (when one was received).

## Failure modes

- A subscription with an unreachable endpoint **MUST NOT** stall the process — it exhausts its budget and is dropped.
- The wrapper **SHOULD** emit logs through `ILogger<HttpWebhookDispatcher>` only — never directly to a sink.

## Non-goals

- No inbound webhook receiving / verification middleware.
- No durable subscription persistence, management HTTP API, or dashboard.
- No delivery-persistence / guaranteed-once semantics — delivery is best-effort at-least-once within the retry budget.
- No per-subscription ordering or delivery-parallelism guarantee.

## See also

- Spec: [Webhooks.spec.md](./Webhooks.spec.md) · folder: [webhooks.md](./webhooks.md)
- Retry contract: [`../Messaging.standard.md`](../Messaging.standard.md) (§Delivery & reliability)
