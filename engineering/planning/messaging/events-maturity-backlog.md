# Events layer — maturity backlog

*Synthesized 2026-07-10 from a 5-agent gap analysis of `src/Messaging/` vs MassTransit · NServiceBus · Wolverine · Rebus · Brighter · DotNetCore.CAP · Dapr.*

> Anti-YAGNI mandate: the messaging layer must be a best-in-class LEGO SDK — every capability an interface + default impl + `.AddX()` seam. This backlog enumerates *everything under the sun* per vector, gap-analyzed against what's built. Provider breadth within one vector (e.g. which DBs back the outbox) is deliberately out of scope; the distinct **capability vectors** are the focus.

## 0. Shape of the work

Three tiers, in priority order:

- **Tier A — correctness bugs** (§1): silent data-loss / silent-failure defects the analysis surfaced. Fix before adding features.
- **Tier B — keystone seams** (§2): ~12 cross-cutting ports; each unblocks a whole vector and turns hard-coded behaviour into swappable lego. Build these next.
- **Tier C — vector breadth** (§3): the exhaustive per-vector capability set, mostly gated behind Tier B seams.

Cross-agent convergence (same defect found from independent angles) is called out — those are the highest-confidence, highest-value items.

---

## Deferred — conscious pushes (tracked here, nothing lost)

Consciously deferred while shipping Waves 0–1; each remains in the tiered backlog below. Log kept explicit so the analysis isn't lost:

| Deferred item | Ref | Why deferred |
|---|---|---|
| A3/A4 — conn auto-recovery + publisher confirms | §1 | need a connection-recovery seam → **Wave 2** |
| A7/A8 — broker delay/TTL/priority mapping + re-enqueue retry | §1 | need delay-topology + scheduler → **Wave 2** |
| Kafka true delivery-count | §1 A2 | baseline is `1`; real count needs a bumped redelivery header → **Wave 2** |
| Outbox + webhooks serializer adoption | §2 | still on direct STJ; thread `IMessageSerializer`/`IMessageTypeResolver` through them |
| Additional serializers (MessagePack/Protobuf/Avro/CBOR/CloudEvents) | §3.2 | now unblocked by the Wave-1 seam |
| Remaining Tier-B seams | §2 | concurrency pump · topology/routing · capabilities-expansion · `IMessagingMetrics` · `IMessageSink` · `IBusControl` · `IRequestClient` · state-machine saga (`IMessageSerializer`/`IMessageTypeResolver`/`IConsumeFilter` ✅ done) |
| Full Tier-C breadth | §3 | ~18 transports · DLQ redrive/2nd-level-retry · EIP patterns · test harness · ops/mgmt |

---

## 1. Tier A — correctness bugs (fix first)

> **Wave 0 shipped 2026-07-10** — A1 (Kafka/NATS unparseable → re-produce to DLQ, RabbitMQ already nacked→DLX) · A2 (delivery-count: NATS native `NumDelivered`, RabbitMQ `Redelivered` flag, Kafka baseline `1` — true count needs a header-bump, deferred to Wave 2) · A5 (outbox `MaxDispatchAttempts` give-up + retention prune) · A6 (`DeadLetterAsync(reason, exception, ct)` — captures exception type; Kafka/NATS stamp `wt-dl-*` death headers) · A9 (`AddMeter("WoW.Two.*")`). Suite: 15 pass / 1 skip; in-mem dead-letter test asserts exception capture. **Remaining: A3/A4 (conn recovery + publisher confirms) · A7/A8 (broker delay/TTL mapping + re-enqueue retry) → Wave 2.**

Analysis-flagged; verify each against current code before fixing, but all cite concrete call sites.

| # | Bug | Evidence | Impact | Fix |
|---|---|---|---|---|
| A1 | **Silent message drop on unresolvable / unparseable message** *(converged: transports + reliability + serialization)* | `Type.GetType(AQN)` → `null` → `TryReconstruct` returns null → Kafka stores-offset / NATS acks / RabbitMQ nacks-no-requeue = permanent loss | Data loss; the message a broker exists to deliver is discarded, not dead-lettered | Route unresolved/unparseable → DLQ with reason; uniform across adapters |
| A2 | **`EventEnvelope.DeliveryCount` hard-coded `0`** at every adapter (`RabbitMq`/`Kafka`/`Nats` `TryReconstruct`) | never populated from real redelivery count | Poison-by-count detection, redelivery-aware backoff, redelivery limits, delivery-count metrics all impossible | Wire transport redelivery count → envelope (keystone for A/retry/metrics) |
| A3 | **Connection death not detected/recovered** (RabbitMQ) | recovery flags unset; receiver parks on `Task.Delay(Infinite)` | A dropped connection **silently stops consumption** — no re-subscribe | Enable auto-recovery + reconnect loop + re-subscribe |
| A4 | **No publisher confirms + no send-side retry** | `SendAsync` publishes direct; RabbitMQ no `ConfirmSelect` | Silent **publish** loss on transient broker fault (at-least-once broken at the producer edge) | Confirm-mode + await + wrap publish in resilience |
| A5 | **Outbox never caps attempts / never prunes** | `Attempts`/`Error` bumped, never capped; `outbox_/inbox_messages` no retention | Poison row retries forever at batch head; unbounded table growth | Max-attempts → outbox-DLQ; pruning sweeper |
| A6 | **Dead-letter loses exception detail** | pipeline calls `DeadLetterAsync(ex.Message)`; `DeadLetterRecord.From` (captures type) is dead code; in-mem hard-codes `ExceptionType: null` | Can't triage a DLQ (no type/stack/attempt) | Widen `DeadLetterAsync(reason, exception)`; capture type+stack+attempt |
| A7 | **Broker delay / TTL / priority silently ignored** | `NativeDelay=false` on all; `RabbitMqSendTransport` never sets `Priority`/`Expiration` despite broker support | `Delay`/`NotBeforeUtc`/`TimeToLive`/`Priority` honored **in-memory only** — a broker deploy loses scheduled/expiring/priority delivery | Map hints per adapter, or fail-loud where unsupported |
| A8 | **In-proc `Task.Delay` retry blocks the consume loop** | resilience pipeline holds the message unsettled through the whole backoff | Blocks Kafka's synchronous consume loop entirely; pins a RabbitMQ prefetch slot → throughput cliff | Re-enqueue-with-delay (see B + delayed-retry) |
| A9 | **SDK meters never collected** | `AddOpenTelemetryMetrics` has no `AddMeter("WoW.Two.*")` (tracing has `AddSource`) | Even the existing errors meter is uncollected; blocks all messaging metrics | One line: `AddMeter("WoW.Two.*")` |

---

## 2. Tier B — keystone seams (build the ports)

> **Wave 1 shipped 2026-07-10** — **`IMessageSerializer`** (default `SystemTextJsonMessageSerializer` through `JsonOptionsPresets`) + **`IMessageTypeResolver`** (`DefaultMessageTypeResolver` — stable `FullName` token via `MessageTypeRegistry` populated from the handler/contract scan; AQN fallback; aliases). `ContentType` on the envelope + `wt-content-type` header. Threaded through **all 3 brokers** (RabbitMQ/Kafka/NATS); in-memory doesn't serialize. Override seams: `AddMessageSerializer<T>` / `AddMessageTypeResolver<T>` / `MapMessageType<T>(token)`. Fixed a latent `JsonOptionsPresets` init crash (missing `TypeInfoResolver`). Suite 15/16 + resolver unit tests 4/4.
>
> **`IConsumeFilter` consume-pipeline shipped 2026-07-10** — `EventProcessingPipeline` restructured into an ordered filter chain wrapping the resilience→dedupe→dispatch core (`ConsumeDelegate` + `IConsumeFilter` + `AddConsumeFilter<T>`; empty chain = behavior-preserving; filters run once per message, first-registered = outermost). Unblocks fault-publish · wire-tap/audit · claim-check · rate-limit · per-consumer breaker · translator · decorators. Suite 20/21; ordering test green. **Remaining Tier-B:** concurrency pump · topology/routing · capabilities-expansion · `IMessagingMetrics` · `IMessageSink` · `IBusControl` · `IRequestClient` · state-machine saga. **Follow-up:** thread the serializer/resolver through the **outbox** + webhooks (still on direct STJ); add MessagePack/Protobuf/Avro/CloudEvents serializers (now unblocked, §3.2).

Each is a swappable port that (a) removes hard-coded behaviour and (b) unblocks a swathe of Tier C. Ordered by downstream leverage.

| Seam | Replaces / enables | Unblocks (Tier C) | Effort |
|---|---|---|---|
| **`IMessageTypeResolver`** (+ `MessageUrn`/logical-name registry, aliases) | `Type.GetType(AssemblyQualifiedName)` (brittle, .NET-only, silent-drop) | cross-service consume · versioning/upcasting · polymorphic messages · A1 | M |
| **`IMessageSerializer`** (+ `ContentType` on envelope + registry/negotiation) | STJ hard-inlined in ~7 sites (4 adapters + outbox + webhooks), bypassing `JsonOptionsPresets` | every serializer (STJ-srcgen/Newtonsoft/MessagePack/Protobuf/Avro/CBOR/BSON/XML/raw/CloudEvents) · compression · encryption · claim-check | M |
| **`IConsumeFilter<T>` consume pipeline** (ordered, pluggable) | fixed `EventProcessingPipeline` | per-consumer retry/rate-limit/breaker · fault-publish · wiretap · claim-check · filter · translator · decorators · transactional consumer · audit sink | L |
| **Concurrency pump** (`ConcurrencyLimit` + N-worker channel; per-key affinity) *(converged: 1+2+4)* | fully sequential consume (prefetch is a no-op) | throughput · ordered-parallel · batch consume · backpressure | M |
| **`ITopologyProvider` + `IEndpointNameFormatter`** (per-type routing) *(converged: 1+5; handoff NEXT)* | `#` catch-all bind, hard-coded inline declares | per-type endpoints · publish/send separation · per-endpoint DLQ · delayed/retry topology · subscriptions · content/header routing | L |
| **`ITransportCapabilities` expansion** (~12 flags) | 4-flag model | drives native-vs-emulated for sessions/FIFO · scheduling≠delay · producer-tx · priority · TTL · confirms · request-reply — prereq for richer adapters | S |
| **`IMessagingMetrics`** (`Meter "WoW.Two.Sdk.Messaging"`) | zero metrics | dashboards · health thresholds · kill-switch · idle-detection · lag | M |
| **`IMessageSink` / observers** (`IReceive/Publish/ConsumeObserver`) | no interception hook | audit store · test harness · diagnostics · fault publishing | M |
| **`IBusControl`** (Start/Stop/Pause/Resume) | host-lifecycle only | ops pause/resume · graceful drain w/ timeout · kill-switch · health-gated startup | M |
| **`IRequestClient<TReq,TResp>`** (+ `ReplyTo` on envelope + correlation + timeout) *(converged: 1+4)* | `ConversationId` carried but inert | request/response · scatter-gather · correlated replies | L |
| **`ISagaRepository<TState>` + state-machine saga + correlate-by-key** | routing-slip only, in-memory, dies on crash | stateful orchestration · timeouts · aggregator · process manager | L (epic) |
| **Typed header model + header-propagation policy** | `string→string` bag; only `traceparent` flows | header routing/filter · message intent · signing · content-encoding · user-header propagation | M |

---

## 3. Tier C — vector breadth (everything under the sun)

Condensed per vector: capability → status (❌ missing · 🟡 partial). Full who-has-it / effort / dependency detail is in the agent analyses (session transcript). Most rows are gated behind the Tier B seam named in each vector's header.

### 3.1 Transport adapters *(seam: port + capabilities + topology, all exist/expanding)*
Build-out (each an independent vector once the port + topology + capabilities stabilize): ❌ **Azure Service Bus** (native DLQ/scheduled/sessions — highest-value single adapter) · ❌ **AWS SQS+SNS** (redrive DLQ + fan-out) · ❌ **Redis Streams** (consumer-groups + PEL; cheap/ubiquitous) · ❌ Azure Event Hubs (+checkpoint store) · ❌ GCP Pub/Sub · ❌ MQTT (QoS 0/1/2, retained, LWT) · ❌ Apache Pulsar (native DLQ + retry-letter + sub-types) · ❌ ActiveMQ/Artemis (AMQP) · ❌ Amazon Kinesis · ❌ **SQL-table-as-queue** (reuses PG skip-locked) · ❌ Postgres LISTEN/NOTIFY · ❌ Azure Storage Queues · ❌ gRPC/HTTP transport · ❌ Amazon EventBridge · ❌ Dapr pub/sub · ❌ **CAP-as-adapter** (consume-bridge is the hard fork) · ❌ **null/blackhole** transport · ❌ partitioned in-memory · ❌ **in-memory test harness** (see 3.12).

### 3.2 Serialization & contracts *(seam: `IMessageSerializer` + `IMessageTypeResolver`)*
Serializers: ❌ MessagePack · ❌ Protobuf · ❌ Avro · ❌ CBOR · ❌ BSON · ❌ XML · ❌ Newtonsoft compat · ❌ raw-bytes · ❌ string · ❌ **CloudEvents** format · 🟡 STJ (not src-gen, bypasses `JsonOptionsPresets`). Type identity: ❌ URN/logical-name · ❌ aliases/redirects · ❌ polymorphic/interface messages · ❌ multi-interface messages · ❌ cross-language identity · 🟡 registry (CLR-`Type`-keyed only). Versioning: ❌ upcasting/downcasting · ❌ translators · ❌ version stamp · ❌ deprecation · ❌ compat test harness. Schema: ❌ registry (Confluent/Apicurio/Glue) · ❌ Avro/Proto/JSON-Schema serdes · ❌ publish/consume validation · ❌ compat checks · ❌ codegen. Payload: ❌ **claim-check** (body always inline; every broker caps size) · ❌ compression (gzip/brotli/lz4/snappy/zstd + content-encoding) · ❌ envelope encryption + KMS + field-level · 🟡 signing (webhook-only) · ❌ size limits/guardrail · ❌ content-hash.

### 3.3 Dead-lettering *(seam: consume pipeline + `IDeadLetterStore` per broker)*
❌ DLQ redrive/replay over **native** broker DLQs (in-mem only today) · ❌ browse/peek · ❌ **second-level retry** (retry→delay→DLQ tiers) · ❌ poison-by-delivery-count (A2) · 🟡 dead-letter reason (A6) · ❌ exception type+stack+attempt on record (A6) · ❌ death headers on the dead-lettered message · ❌ per-type/per-endpoint DLQ · ❌ skipped-vs-error queue split (no-handler path silently acks today) · ❌ unparseable→DLQ (A1) · ❌ DLQ TTL/quarantine · ❌ dead-letter metric/observer · ❌ manual move-to-DLQ.

### 3.4 Retry & circuit protection *(seam: consume pipeline + resilience)*
❌ per-exception-type policies · ❌ Ignore/Retry exception **filters** (every exception burns MaxAttempts today) · ❌ immediate-DLQ for non-retryable · ❌ **delayed/scheduled retry via re-enqueue** (A8) · ❌ redelivery-count-aware backoff (A2) · ❌ retry-vs-redelivery split · ❌ retry budget/rate-cap · ❌ per-endpoint retry config · 🟡 circuit breaker (Polly-only, global) · ❌ per-consumer breaker · ❌ **consume concurrency limiter** · ❌ rate-limit on consume · ❌ bulkhead · ❌ kill-switch · ❌ consumer pause/resume · 🟡 backpressure (in-mem only) · ❌ adaptive concurrency · ❌ give-up-by-deadline · ❌ retry metric.

### 3.5 Delivery semantics *(seam: capabilities + `ReceiveContext` primitives)*
❌ Kafka EOS transactional producer · ❌ ordered/keyed consumers (`NativeOrdering` flag never enforced) · ❌ publish-side dedup window (`Nats-Msg-Id`/ASB `MessageId` unset) · ❌ dedup-key strategies (inbox is MessageId-only) · ❌ native scheduled/delayed delivery (A7) · ❌ message TTL/expiry (A7) · ❌ priority (A7) · ❌ sessions/FIFO groups · ❌ deferred messages · ❌ negative-ack/requeue on `ReceiveContext` · ❌ defer/reschedule · ❌ batch ack · ❌ partial-batch failure · ❌ lock renewal for slow handlers · ❌ SDK-enforced redelivery limits.

### 3.6 Outbox / inbox depth *(seam: existing `IOutbox`/`IOutboxClaimStrategy`)*
❌ outbox poison give-up/DLQ (A5) · ❌ outbox/inbox pruning (A5) · ❌ idempotent producer / publish dedup · ❌ batch publish (row-by-row today) · 🟡 outbox ordering (no per-aggregate FIFO) · ❌ scheduled outbox (respect `NotBeforeUtc`) · ❌ outbox pending-gauge/oldest-age metric · ❌ transactional-session outbox (enlist ambient tx) · ❌ **per-consumer inbox scoping** — the inbox dedup key is `message_id`-only (`EfInboxProcessor` PK), correct only when each service owns its own inbox DB; mature = **same table, composite `(consumer_id/endpoint, message_id)`** (MassTransit `InboxState` keyed by `(MessageId, ConsumerId)`; NServiceBus per-endpoint). Required for multiple consumers sharing one inbox DB **or** per-handler once-only (handler A committed / B failed → retry only B). *(Provider breadth — SqlServer/Mongo/… claim strategies — is out of scope per mandate; the abstraction exists.)*

### 3.7 Sagas / orchestration *(seam: `ISagaRepository<TState>` epic)*
❌ **state-machine saga** (Initially/During/When→TransitionTo) · ❌ **persistent saga state + repository** (abstraction; in-mem default + EF/Mongo/Redis impls) · ❌ **correlate-by-key** · ❌ saga lifecycle initiate/orchestrate/finalize · ❌ saga timeouts / schedule-to-self · ❌ saga concurrency (optimistic/pessimistic) · ❌ event-sourced saga · ❌ saga versioning · ❌ state-machine compensation · ❌ **routing-slip durability** (current runner dies on crash) · ❌ context hydration/dehydration · ❌ process manager · ❌ saga finalization/retention · ❌ saga query/inspection · ❌ saga observers.

### 3.8 Enterprise integration patterns *(seam: consume pipeline + request-client + saga)*
❌ **request/response** (`IRequestClient`) · ❌ scatter-gather · ❌ aggregator · ❌ splitter · ❌ **claim-check** · ❌ content-based router · ❌ message filter · ❌ message translator · ❌ wiretap · ❌ recipient list · 🟡 **timeout/scheduling manager** (one-shot in-mem only; no self-schedule, no cron/recurring — bridge to `src/Jobs/` Hangfire+Cronos) · ❌ delayed redelivery · ❌ resequencer · ❌ normalizer · ❌ return-address/reply-to on envelope.

### 3.9 Consumer model *(seam: consume pipeline)*
❌ consumer middleware/filter pipeline · ❌ consumer definitions + per-consumer config · ❌ lifecycle hooks · ❌ **batch consumers** · 🟡 keyed/ordered/partitioned handlers · ❌ consumer observers · ❌ concurrency control · ❌ rate-limit per consumer · ❌ per-consumer breaker · 🟡 transactional consumer (inbox-only). Handler discovery: ❌ convention-based scan · ❌ keyed handlers · ❌ open-generic handlers · ❌ **handler ordering** · 🟡 **fan-out control** (sequential/registration-order/stop-on-first-throw only — no parallel/`WhenAll`/aggregate-exception) · ❌ delegate/lambda handlers · ❌ `IEventContextAccessor` (ambient) · ❌ handler decorators.

### 3.10 Observability *(seam: `IMessagingMetrics` + observers)*
Metrics: ❌ throughput counters · ❌ consume-duration/handler-latency histograms · ❌ retry-count · ❌ DLQ/fault-rate · ❌ in-flight gauge · ❌ consumer-lag/queue-depth · ❌ delivery-count distribution · ❌ meter + collection wiring (A9). Tracing: 🟡 spans exist but omit `messaging.system`/`operation.type`/consumer-group/body-size · ❌ error-status on faulted span · ❌ span links (batch) · ❌ baggage. Diagnostics: ❌ observer/`DiagnosticSource` events · ❌ **message audit store** · ❌ message sink · 🟡 `[LoggerMessage]` (producer path unlogged) · ❌ correlation log scopes.

### 3.11 Operations / management *(seam: `IBusControl` + observers)*
❌ replay/reprocess API (broker DLQs) · ❌ DLQ browse+redrive · ❌ **pause/resume/stop consumer at runtime** · ❌ peek/browse · ❌ purge · ❌ endpoint monitoring · 🟡 topology viz (saga `ToMermaid` only) · ❌ **per-transport health checks** (broker-reachable / consumer-running / DLQ-depth / outbox-backlog) · ❌ readiness-vs-liveness.

### 3.12 Testing *(seam: `src/Testing/` + message sink)*
❌ **consumer-facing test harness** (`MessagingTestHarness` — today every test re-rolls `EventCollector`) · ❌ published/sent/consumed/faulted assertions · ❌ consumer test harness · ❌ saga test harness · ❌ inactivity/idle-wait primitive · 🟡 time control (`TimeProvider` injected, no `FakeTimeProvider` surface) · ❌ message faker.

### 3.13 Configuration & lifecycle
❌ options validation on bus adapters (`ValidateOnStart` — Webhooks does it, adapters don't) · ❌ `IConfiguration` binding · ❌ multi-env · ❌ secret/connection management (platform owns `secrets-vault`) · 🟡 feature toggles (via `Add*` composition). Lifecycle: 🟡 graceful drain (no stop-timeout / in-flight await) · 🟡 startup ordering (implicit) · ❌ start/stop observers · ❌ health-gated startup · ❌ kill-switch. Connection (transport): ❌ failover/multi-endpoint · ❌ reconnect backoff · ❌ heartbeats config · ❌ wait-for-broker · ❌ channel pooling (single gated channel = publish bottleneck) · ❌ TLS/SASL/managed-identity auth surface · ❌ blocked-connection alarms.

---

## 4. Recommended sequence (waves)

1. **Wave 0 — correctness (§1):** ✅ **DONE 2026-07-10** — A1 · A2 (Kafka true-count deferred) · A5 · A6 · A9. A3/A4/A7/A8 moved to Wave 2 (need conn-recovery / delay-topology seams).
2. **Wave 1 — foundational seams (§2):** `IMessageSerializer`+`IMessageTypeResolver` (fixes A1 properly, unblocks all serialization) · `IConsumeFilter` pipeline (unblocks reliability + patterns + audit) · concurrency pump (unblocks throughput) · `ITransportCapabilities` expansion.
3. **Wave 2 — reliability & routing depth:** exception-aware retry + delayed/2nd-level retry (fixes A8) · DLQ redrive + per-broker `IDeadLetterStore` · topology port + per-type routing (A7 mapping) · connection recovery + publisher confirms (A3/A4) · `IMessagingMetrics` suite + health checks.
4. **Wave 3 — patterns & orchestration:** `IRequestClient` request/response · state-machine saga + `ISagaRepository` epic · aggregator/scatter-gather/claim-check · `IBusControl` + pause/resume/drain · test harness.
5. **Wave 4 — adapter & format breadth (parallelizable once seams stable):** ASB → SQS+SNS → Redis Streams → … (3.1) · MessagePack/Protobuf/Avro/CloudEvents (3.2) · schema registry · encryption/signing.

See also: [`handoff.md`](./handoff.md) · [`messaging-architecture-investigation.md`](./messaging-architecture-investigation.md) · `../platform-planning.md`.
