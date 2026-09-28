# Webhooks completion — build track

*Last updated: 2026-09-28*

> Completes the webhooks vector beyond the July outbound sink: receiving, durable subscriptions, and management.
> Outbound delivery (HMAC, SSRF guard, bounded retry) shipped 2026-07-10 in `src/Messaging/Webhooks/`.

## Rulings — 2026-09-28

- Receiving validates the raw body, so the handler reads the payload from `WebhookReceiptModel`, never a bound body.
- One validator per scheme, keyed by the scheme name; products add schemes by registration, not by options.
- Replay protection: the signed timestamp within `Tolerance` where the scheme signs one; delivery-id deduplication
  everywhere, through the shared `IIdempotencyRepository` (in-memory, SQL or Redis).
- Only a handled success is remembered, so a failed attempt stays retryable by the sender.

## Status

- [x] W1 — inbound: `AddInboundWebhooks`, `RequireWebhookSignature`, 8 schemes; published Standard, GitHub and Slack
  vectors pass; outbound-signed `wow2` deliveries validate; endpoint dedupe and retry proven
- [x] W2 — durable subscriptions: `EfWebhookSubscriptionRepository` over `webhook_subscriptions`, Data-Protection
  secrets, enable/disable, find/list/remove on the repository contract
- [x] W3 — delivery log: `EfWebhookDeliveryRepository` over `webhook_deliveries` with kept bodies, history, purge and
  `WebhookRedeliveryService`; `X-Webhook-Id` now stays the same across retries
- [ ] W4 — management API: map subscription CRUD, secret rotation and delivery history for a subscription owner
- [ ] W5 — outbound Standard Webhooks signing as a selectable scheme beside `sha256=`

---

## W1 — Inbound

- Receivers under `Webhooks:Inbound:Receivers:{name}`: `Scheme`, `Secrets`, `Tolerance`, `Deduplicate`, `MaxBodyBytes`.
- Acceptance: forged, stale, unsigned and oversized deliveries never reach the handler; a repeat answers with the
  first status; a failed attempt runs again.

## W2–W4 — Durable outbound

- Open questions for the owner before W4: who owns a subscription (user, tenant, API key) and how secrets are shown.

## W2–W3 — Defect fixed

- The dispatcher minted a new `X-Webhook-Id` per attempt, so a receiver could not drop a retried delivery.
