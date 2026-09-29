# Backend Beta SDK — Backlog

*Last updated: 2026-09-29*

Every unbuilt item, grouped; top of each group = next. Shipped capabilities live in
[`package-registry.md`](../architecture/package-registry.md) and git; deep-dives in [`research/`](../research/research.md).
The SDK versions as NuGet `10.y.z-beta`, so a feature reads `shipped` once the published family (`10.0.60-beta`) carries it.

## Features

| Feature | State | Spec |
|---|---|---|
| One-call API defaults (`AddApiDefaults`) | shipped | — |
| Test hosts, container fixtures, fakes and harnesses | shipped | [Testing.spec.md](../codebase/wow-two-back-beta-sdk/src/Testing/Testing.spec.md) |
| Errors, results and exception mapping | shipped | — |
| Validation with advisories and translated messages | shipped | — |
| Serialization presets and stored JSON profiles | shipped | — |
| Time, identifier, naming and configuration primitives | shipped | — |
| Envelope encryption | shipped | [Security.spec.md](../codebase/wow-two-back-beta-sdk/src/Foundation/Security/Security.spec.md) |
| Hash-chained audit trail | shipped | [HashChain.spec.md](../codebase/wow-two-back-beta-sdk/src/Foundation/Audit/HashChain.spec.md) |
| Logging, tracing, metrics and health checks | shipped | — |
| Proxy-aware hosting and request limits | shipped | — |
| OpenAPI and ProblemDetails responses | shipped | — |
| Secure headers, CORS and SPA antiforgery | shipped | — |
| Rate limits, usage quotas and captcha | shipped | — |
| Idempotency keys, conditional requests and paging | shipped | — |
| Request context: language and device | shipped | [RequestContext.spec.md](../codebase/wow-two-back-beta-sdk/src/Web/RequestContext/RequestContext.spec.md) |
| Mediator with CQRS markers and behaviors | shipped | [Mediator.spec.md](../codebase/wow-two-back-beta-sdk/src/Mediator/Mediator.spec.md) |
| Transactional mediator requests | shipped | [data-units.spec.md](../codebase/wow-two-back-beta-sdk/src/Mediator/DataUnits/data-units.spec.md) |
| Feature flags and gates | shipped | — |
| JWT, cookie and OIDC authentication | shipped | — |
| OAuth sign-in with claim normalization | shipped | [ClaimNormalization.spec.md](../codebase/wow-two-back-beta-sdk/src/Identity/Claims/ClaimNormalization.spec.md) |
| Authorization policies and allowlists | shipped | [Authorization.spec.md](../codebase/wow-two-back-beta-sdk/src/Identity/Authorization/Authorization.spec.md) |
| User accounts: passwords, email, lockout, roles, 2FA, phone, passkeys | shipped | — |
| Account HTTP API and passwordless email sign-in | shipped | — |
| OTP delivery over Telegram, SMS, WhatsApp and email | shipped | — |
| API keys | shipped | [ApiKeys.spec.md](../codebase/wow-two-back-beta-sdk/src/Identity/ApiKeys/ApiKeys.spec.md) |
| Guest sessions | shipped | [GuestSession.spec.md](../codebase/wow-two-back-beta-sdk/src/Identity/Guest/GuestSession.spec.md) |
| EF Core and Dapper persistence | shipped | — |
| SQL migrator and `wow-migrate` CLI | shipped | — |
| Data sessions and nested units | shipped | [sessions.spec.md](../codebase/wow-two-back-beta-sdk/src/Data/Sessions/sessions.spec.md) |
| Entity specs, concurrency tokens and transaction locks | shipped | — |
| Caching with commit-aware invalidation | shipped | — |
| Resilient, destination-safe outbound HTTP | shipped | [safety.spec.md](../codebase/wow-two-back-beta-sdk/src/Http/Safety/safety.spec.md) |
| Event bus with outbox, inbox, sagas and dead-letter admin | shipped | [Messaging.spec.md](../codebase/wow-two-back-beta-sdk/src/Messaging/Messaging.spec.md) |
| Webhooks: outbound, inbound and durable subscriptions | shipped | [Webhooks.spec.md](../codebase/wow-two-back-beta-sdk/src/Messaging/Webhooks/Webhooks.spec.md) |
| Background jobs and batch pipelines | shipped | — |
| Email templates, SMS, WhatsApp and push | shipped | — |
| Blob storage with signed URLs | shipped | — |
| Image and PDF tools | shipped | — |
| Word, Markdown, CSV and XLSX documents | shipped | — |
| Captions and YouTube link parsing | shipped | — |
| QR and barcode rendering | shipped | [Rendering.spec.md](../codebase/wow-two-back-beta-sdk/src/Codes/Rendering/Rendering.spec.md) |
| Scanner payload encoders | shipped | [Payloads.spec.md](../codebase/wow-two-back-beta-sdk/src/Codes/Payloads/Payloads.spec.md) |
| Geo primitives and IP location | shipped | — |
| Realtime over SignalR, SSE and WebSockets | shipped | — |
| Request localization and formatters | shipped | — |
| GitHub and container-registry clients | shipped | — |
| Per-row multi-tenancy | shipped | — |
| AI chat and embeddings over Ollama | shipped | — |
| Payments | planned | — |
| Search | planned | — |
| Workflow | planned | — |

---

## Vectors

SDK vectors without a group of their own below; each row is the vector's next completion pass.

| Item | Type | Notes |
|---|---|---|
| AI providers, SSE streaming and pgvector | feature | TranscriptForge pulls it forward; the `Microsoft.Extensions.AI` preview pin and OllamaSharp move together (owner decision) |
| Payments, Stripe first | feature | ForeverPin billing and TranscriptForge wait on it — [D04](../research/foreverpin-adoption/foreverpin-adoption.md) |
| Tenancy strategies: Finbuckle, per-database, per-schema | feature | Per-row isolation ships |
| Feature-flag providers: LaunchDarkly, ConfigCat, Unleash, GrowthBook | feature | Flags, gates and the OpenFeature seam ship |
| FusionCache adapter | feature | Hybrid, memory and Redis caches ship with commit-aware invalidation |
| Code output: barcode and per-eye styling, logos, PDF and print | feature | ForeverPin deferral [D09](../research/foreverpin-adoption/foreverpin-adoption.md) |
| Geo: NetTopologySuite, geocoding, H3 and paid IP databases | feature | Free DB-IP Lite ships; paid databases wait on licensing — [E3](../research/foreverpin-extractions/foreverpin-extractions.md) |
| Realtime scale-out: SignalR Redis backplane and Azure SignalR | feature | SignalR, SSE and WebSockets ship |
| Search, PostgreSQL full-text first | feature | [targets](../architecture/analysis/philosophy/targets.md) §3.15 |
| Workflow, Stateless first | feature | [targets](../architecture/analysis/philosophy/targets.md) §3.10 |
| Google Cloud Storage adapter | feature | Local, S3 and Azure storage ship |

---

## Data

| Item | Type | Notes |
|---|---|---|
| Full-row reads and guarded writes in data sessions | feature | `FullRow<T>`, `LoadForUpdate`, write guards A/B, EF converter reflection — phase 3 of the [architecture](../research/data-pipeline/data-session-architecture.md); [test plan](../research/data-pipeline/data-session-test-plan.md) |
| Soft-delete, tenant and xmin fragments for hand-written Dapper SQL | feature | `SqlRead.Filters<T>()` and `Columns<T>()` over an EF-projected read profile; generated reads already filter |
| Manual flush and EF savepoint hooks in data sessions | feature | Nested rollback clears the tracker and needs a reload |
| Cache eviction from data-session commit hooks | feature | Verify HybridCache cross-node tag invalidation first — [research 02](../research/data-pipeline/research/02-transactional-cache-coherence.md) |
| NHibernate mapper for entity specs | feature | EF Core and Dapper mappers ship — [entity specs](../research/data-pipeline/entity-specs.md) |
| Partial updates and a second `DbContext` per session | feature | Deferred by the owner's fixed constraints |
| Mine Marten for the session and hook design | engineering | [Verdict](../research/data-pipeline/data-pipeline-verdict.md): Marten and Wolverine cover most of it, on a document store |
| Benchmark attach, savepoints and HybridCache reads | engineering | The attach win is unmeasured; no BenchmarkDotNet yet |

---

## Messaging

| Item | Type | Notes |
|---|---|---|
| AWS SQS and SNS transport | feature | Redrive DLQ, FIFO groups, visibility-timeout settlement — [maturity](../research/messaging/events-maturity-backlog.md) §3.1 |
| RabbitMQ unroutable-publish detection | feature | Opt-in `mandatory` returns, or an alternate exchange; weigh the exchange first |
| Persistent saga repositories: EF Core and Redis | feature | Only the in-memory repository ships — [maturity](../research/messaging/events-maturity-backlog.md) §3.7 |
| Pause Kafka partitions natively | fix | Parking the consume thread risks a `max.poll.interval.ms` eviction |
| Upgrade RabbitMQ.Client past 7.0.0 | fix | 7.0.0 skews publish sequence numbers on concurrent failure |
| Test and document saga destination bindings | engineering | Pin the anti-steal alias rule, document `SendsTo`, fix stale send-transport header comments |
| Run the Kafka dead-letter test on Linux CI | engineering | Skipped for a macOS librdkafka crash; the header-forgery check needs a raw consumer |
| Test the Service Bus adapter on its emulator | engineering | Its adapter-owned header round trip is unproven |
| Per-consumer inbox scoping | feature | Composite consumer and message key for shared inbox databases — [maturity](../research/messaging/events-maturity-backlog.md) §3.6 |
| Opt-in serializer seam for webhooks | feature | Webhooks stay off the seam today: presets would change every signed byte |
| Webhook management API | feature | Subscription CRUD, secret rotation, delivery history; the owner decides who owns a subscription — [webhooks](../research/messaging/webhooks.md) |
| More transports: Event Hubs, Pub/Sub, MQTT, Pulsar, SQL-table queue | feature | CAP, MassTransit and Wolverine only as adapters under the port — [maturity](../research/messaging/events-maturity-backlog.md) §3.1 |
| Protobuf and Avro serializers, versioning and a schema registry | feature | MessagePack and CloudEvents ship — [maturity](../research/messaging/events-maturity-backlog.md) §3.2 |
| Payload compression and envelope encryption | feature | Rides `EventEnvelopeModel.RawBody` — [maturity](../research/messaging/events-maturity-backlog.md) §3.2 |
| Aggregator, scatter-gather, splitter and resequencer | feature | Request client and claim-check ship — [maturity](../research/messaging/events-maturity-backlog.md) §3.8 |
| Durable routing slips | feature | The routing-slip runner dies with its process — [architecture](../research/messaging/messaging-architecture-investigation.md) |
| Batch consumers and parallel fan-out | feature | [maturity](../research/messaging/events-maturity-backlog.md) §3.9 |
| Per-transport health checks and consumer-lag metrics | feature | [maturity](../research/messaging/events-maturity-backlog.md) §3.10–3.11 |
| Adapter options validation, failover and TLS/SASL settings | feature | [maturity](../research/messaging/events-maturity-backlog.md) §3.13 |
| Kafka exactly-once, publish-side dedupe and lock renewal | feature | [maturity](../research/messaging/events-maturity-backlog.md) §3.5 |
| AsyncAPI documents | feature | [architecture](../research/messaging/messaging-architecture-investigation.md) §6 |
| Elastic pipeline steps | feature | Idea only; a configured per-step mode first — [idea](../research/messaging/elastic-pipeline-steps.md) |

---

## Identity

| Item | Type | Notes |
|---|---|---|
| Organizations and invitations | feature | Needs the owner's model — [SaaS essentials](../research/sdk-completion/saas-essentials.md) |
| Verify six best-effort OAuth claim profiles | engineering | X, Yandex, VK, Amazon, Slack, Notion against each library's `ClaimActions` — [identity](../research/identity/identity-architecture.md) |
| Register the OAuth baseline vectors in `targets.md` | engineering | Claim normalizer, cookie mode, allowlist and default-deny |

---

## Errors

| Item | Type | Notes |
|---|---|---|
| Error-code analyzer | feature | [errors](../research/errors/errors-architecture-investigation.md) |
| `HelpLink` and remediation guidance on `AppError` | feature | `HelpLink` maps to the ProblemDetails `type` |
| Settle the ProblemDetails `type` URI scheme | engineering | URNs today |
| Rename `FieldError` to `ValidationFailure` | engineering | Breaking; consumers re-pin |

---

## Media

| Item | Type | Notes |
|---|---|---|
| Word template conditionals and paragraph loops | feature | `{{#if}}` and `{{#each}}` blocks — [documents](../research/media/documents.md) |
| PDF to images companion over PDFium | feature | Parked on native size, about 10 MB per platform — [images and PDF](../research/media/images-and-pdf.md) |
| PDF Bates numbering, redaction, flattening and OCR | feature | Inline in pdf-editor today; flattening exists nowhere |

---

## Quality

| Item | Type | Notes |
|---|---|---|
| Component registry: registration ledger and slot-drain checks | engineering | Record skipped `TryAdd` and `Replace` calls; fail on undrained slots — [verdict](../research/component-registry/component-registry-idea.md) |
| Roslyn analyzer: Foundation imports no domain package | engineering | Pending decision 9.14 in [targets](../architecture/analysis/philosophy/targets.md) |
| Aspire playground end-to-end smoke | engineering | Nothing runs the wrappers end to end |
| Cap decompressed request bodies | fix | Request limits bound only the compressed input |

---

## Adoption

| Item | Type | Notes |
|---|---|---|
| Re-pin the direct consumers to the latest beta | engineering | TransportBrain, Tnis, TnisMintrans, Wheelhouse, SecretsVault, ListingShelf, Sift, Arcade, MuseumsGallery, TranscriptForge (plus its caption-parser namespace), product template; ForeverPin re-verifies on published `10.0.60-beta` |
| Dogfood the identity slices in Haven.Auth | product | Identity build step 10 — [identity](../research/identity/identity-architecture.md) |
| Adopt `Media.Pdf` and `Media.Word` in pdf-editor | product | The extraction source proves parity first — [images and PDF](../research/media/images-and-pdf.md) |
