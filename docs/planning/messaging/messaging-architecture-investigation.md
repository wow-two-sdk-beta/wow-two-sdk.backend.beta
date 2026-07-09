# Messaging, Events & Sagas — architecture investigation

*Last updated: 2026-07-08 · Status: **event-centric rename + first code shipped (green build)** — see Review log; broker/CAP adapters + outbox dispatcher pending*

> Design of the SDK event-messaging layer (`WoW.Two.Sdk.Backend.Beta.Messaging`). Pairs with the in-process `Mediator` (same handler ergonomics, different delivery guarantee) and the `Jobs` layer (outbox-as-job). Mirrors the format of `docs/planning/errors/errors-architecture-investigation.md`.
>
> **Scope:** `src/Messaging/` was empty; the platform `comms.infra` repo is a bare `.sln` — **no prior code, no app consumers yet** (greenfield, per the brief). 6 parallel agents deep-dived the .NET messaging ecosystem against primary sources (masstransit.io, docs.particular.net, wolverinefx.net, cap.dotnetcore.xyz, broker docs, OTel semconv, Polly/Aspire). `targets.md §3.5 / §2.23` already set the high-level verdicts (CAP=core, MassTransit=adapter-only, Stateless=saga lib, brokers=adapters); this doc turns those into a concrete clean-room **abstraction + ports + declarative saga API**, and records what was built.

---

> **Review log — 2026-07-08** (supersedes §1/§4/§5 naming where they differ; body predates the event-centric rename)
> - ✅ **Contract naming = role, never topology.** Two orthogonal axes exist — *scope* (service-local vs cross-service) and *delivery* (in-process vs out-of-process) — and they're independent: a service-local job can still be out-of-process (e.g. long-running file creation → queue + worker, not an in-proc command). Since delivery is a wiring choice and scope is a routing choice, **the event contract encodes neither.** Rejected `IIntegrationEvent`/`IMessage` (topology/abstract). The domain-vs-integration-event distinction is really *audience + contract-stability*, a product-modelling concern — not something the SDK contract bakes in.
> - ✅ **Bus is `IEvent`-only.** `IMessage` + `ICommand` dropped from the bus. The bus enforces `IEvent` via generic constraint (`where TEvent : class, IEvent`). Commands/queries stay on the in-process mediator (`IQuery`/`ICommand`/`INotification`). Imperative bus intent = event-carried (`PaymentRequested`, not `ChargePayment`). Mediator↔bus **merge deferred** (services consume the bus and must not inject the mediator — that coupling blocks the merge for now).
> - ✅ **Full event-centric rename (built, green):** `IMessageBus`→`IEventBus` · `IMessageHandler<T>`→`IEventHandler<TEvent>` · `MessageContext<T>`→`EventContext<TEvent>` (`.Message`→`.Event`) · `MessageEnvelope`→`EventEnvelope` · `IMessageScheduler`→`IEventScheduler` · `AddInMemoryMessaging`→`AddInMemoryEventBus`. Reliability infra (`IRetryPolicy`/`IDeadLetterStore`/`IInboxStore`/`IOutbox`) kept — role-neutral. Folder area stays `Messaging`.
> - ✅ **Events carry transport-abstract options** (`PublishOptions`/`SendOptions` + `EventEnvelope`): `Durable`, `Priority`, `TimeToLive`, `PartitionKey`, `Delay`, `Headers`. Adapters map them to native queue/exchange/topic primitives (RabbitMQ delivery-mode/priority/TTL, Kafka partition, ASB TTL); in-memory ignores what it can't emulate. `SendAsync` 2-form (with/without options) satisfies "send via queue vs direct".
> - ✅ **Saga → `EventSaga*`** (`EventSagaBuilder`/`IEventSagaStep`/`EventSagaRunner`/`EventSagaContext`/`IEventSagaTransport`). Reserves plain `Saga` for a future non-event domain. Folder `Messaging/EventSaga/`.
> - ✅ **Pluggable EF interceptors (built):** `AddEfSaveChangesInterceptor<T>()` / `AddEfInterceptor<T>()` register under `IInterceptor`; `AddEntityFrameworkCore<TContext>` auto-wires **all** registered interceptors into every SDK context → a concern plugs its interceptor from the persistence *or* its own (messaging) registration. Additive/non-breaking (existing audit/soft-delete keep manual wiring; not registered as `IInterceptor`, so no double-add).
> - ✅ **`IHasDomainEvents` scrapped.** No aggregate marker, no entity-event scanning (no clear owner, no benefit). Events are staged **explicitly**: `IOutbox.EnqueueAsync(record)` → `EfOutbox<TContext>` **direct-adds** an `OutboxMessageEntity` to the tracked context → commits atomically on the app's `SaveChanges` (no interceptor needed for staging). Shipped: `OutboxMessageEntity` + `ApplyOutboxModel()` + `AddEfOutbox<TContext>()` + bespoke migration (`Ef.md`).
> - ✅ **Read/write repo split — built (non-enforcing).** Extracted `IWriteRepository<TEntity, in TId>`; `IRepository = IReadRepository + IWriteRepository` (union kept, back-compat). New: `AddEfWriteRepositories<TContext>()` (writes→EF, open) · `AddDapperReadRepository<TEntity,TId>()` (reads→Dapper, closed, read-only bind) · `AddCqrsRepository<TEntity,TId,TContext>()` (turnkey per entity). Additive — existing `AddEfRepositories`/`AddDapperRepository` untouched; apps opt in.
> - ✅ **Outbox dispatcher + EF inbox — built.** `OutboxDispatcher<TContext>` (poll pending → resolve type → JSON-deserialize → typed-publish via cached compiled delegate `OutboxEventPublisher` → stamp `ProcessedOnUtc`) + `OutboxDispatcherHostedService<TContext>` + `AddEfOutboxDispatcher<TContext>()`. `EfInboxStore<TContext>` (`inbox_messages` PK-claim) + `ApplyInboxModel()` + `AddEfInbox<TContext>()`.
> - ✅ **Granular event-infra registration.** Decomposed `AddInMemoryEventBus` into composable `AddInMemoryEventTransport` · `AddInMemoryReliability` · `AddEventHandlersFromAssemblies` (incremental, shared registry) + kept the batteries-included convenience.
> - ✅ **OTel spans (built).** `WoW.Two.Messaging` `ActivitySource` — PRODUCER span on publish (injects W3C `traceparent`/`tracestate` into `EventEnvelope.Headers`), CONSUMER span on process (extracts → parent). `AddOpenTelemetryTracing` now `AddSource("WoW.Two.*")` (wildcard, auto-collects all brand sources).
> - ✅ **Polly resilience (built).** Since `IRetryPolicy.NextDelay` is *stateless*, Polly plugs at an execution seam: `IEventResiliencePipeline` (owns the retry loop, throws when exhausted → dead-letter). Default = `IRetryPolicy`-backed (`DefaultEventResiliencePipeline`); `AddPollyEventResilience(…)` swaps in Polly (exp+jitter + optional breaker + timeout). Consumer refactored onto the seam. Polly.Core 8.4.2 is transitively compile-available (no new package ref).
> - ✅ **Exactly-once inbox (built).** `IInboxStore` (best-effort, mark-then-crash window) → `IInboxProcessor.ProcessOnceAsync(id, handler)`: dedupe **and** run the handler in **one seam**. `EfInboxProcessor<TContext>` opens a tx, inserts the inbox row (PK-conflict ⇒ duplicate), runs the handler (its EF writes enlist), commits both — true exactly-once (handler fault → rollback → retry). Consumer resolves `IInboxProcessor` per-scope (shares the message's `TContext`/transaction with the handler's repos). `InMemoryInboxProcessor` = check + mark-on-success (retry-safe).
> - ✅ **Multi-instance dispatch — contract established.** `IOutboxClaimStrategy.ClaimPendingAsync(context, batchSize)` seam; default `PollingOutboxClaimStrategy` (single-instance). The Postgres `FOR UPDATE SKIP LOCKED` impl is a drop-in `Replace` (spawned as a task).
> - ✅ **Broker order decided: RabbitMQ-first; transport-port contracts shipped.** CAP is a *framework* (outbox+bus+dispatch over a transport + DB), a **peer to our layer**, not a transport — building CAP-first would make our ports thin CAP wrappers (its attribute-`[CapSubscribe]` doesn't fit `IEventHandler<T>`) and cap pluggability to CAP's providers. RabbitMQ is a real transport → establishes **our** port, and CAP/Kafka/ASB (and CAP itself) plug **under** it later. Shipped `src/Messaging/Transport/TransportContracts.cs`: `ISendTransport` · `IReceiveTransport` · `ReceiveContext` (abstract: `Envelope`/`AcknowledgeAsync`/`DeadLetterAsync`) · `ITransportCapabilities` (native DLQ/delay/dedupe/ordering flags).
> - ✅ **Pipeline extracted + in-memory re-seated behind the port.** `EventProcessingPipeline` (transport-agnostic: consumer span → resilience → inbox → dispatch → `ctx.Ack`/`ctx.DeadLetter`) + `TransportConsumerHostedService` (drives any `IReceiveTransport`). Dispatch types (`EventDispatcher`/`Registry`) + the bus (`InMemoryEventBus`→**`TransportEventBus`**, transport-agnostic) moved to `Transport/`. In-memory now = `InMemorySendTransport`/`InMemoryReceiveTransport`/`InMemoryReceiveContext` behind `ISend`/`IReceiveTransport` — the port is proven with a real impl. Reliability split: `AddEventResilienceDefaults` (retry/resilience/inbox, transport-neutral) vs `AddInMemoryReliability` (adds in-mem DLQ/scheduler).
> - ✅ **RabbitMQ adapter built (compile-verified).** `Messaging/RabbitMq/` — `RabbitMqSendTransport` (topic exchange, JSON body, `wt-event-type` + `traceparent` headers, `Persistent`=Durable), `RabbitMqReceiveTransport` (declares exchange + **native DLX/DLQ** + queue, manual-ack consume → pipeline), `RabbitMqReceiveContext` (ack / nack→DLX), `RabbitMqCapabilities` (NativeDeadLetter=true), `AddRabbitMqEventBus(…)`. `RabbitMQ.Client` 7.x pkg ref added to the mono-lib. Same `IEventHandler<T>` handler contract as in-memory — zero handler-side change. **Not yet runtime-verified** (no live broker).
> - ✅ **Runtime-verified (5/5 tests pass).** `Messaging.Tests/` (Testcontainers): in-memory round-trip + dedupe + dead-letter, **RabbitMQ round-trip + native DLX/DLQ over a real broker**. Test host = `Host.CreateApplicationBuilder` + `AddInMemoryEventBus`/`AddRabbitMqEventBus` + a collector handler. Proves #1 (pipeline + re-seat) + #2 (RabbitMQ) end-to-end.
> - ✅ **Kafka adapter built (2nd broker) — exercises the emulated-DLQ corner.** `Messaging/Kafka/` — Kafka has **no native DLQ**, so `KafkaCapabilities.NativeDeadLetter=false` and `KafkaReceiveContext.DeadLetterAsync` **re-produces to a dead-letter topic** then advances the offset (vs RabbitMQ's native DLX). `KafkaSendTransport` (produce, `wt-event-type`/`wt-message-id`/`traceparent` headers, key=partition/msg-id) + `KafkaReceiveTransport` (subscribe, poll loop, `StoreOffset` on settle, `EnableAutoCommit`+`EnableAutoOffsetStore=false`) + `AddKafkaEventBus`. `Confluent.Kafka` pkg ref. **Same `IEventHandler<T>` contract; only `ReceiveContext.DeadLetter` differs** — the capability-flag design is proven. **Kafka round-trip runtime-verified** (Testcontainers, `confluentinc/cp-kafka:7.5.0`, publish→consume→handle). Two real bugs found + fixed via the tests: (1) `TransportConsumerHostedService.StopAsync` disposed the transport *before* draining the consume loop → Kafka's native consumer closed mid-`Consume` → crash (order swapped); (2) `StoreOffset` ran on a thread-pool continuation ≠ the `Consume` thread (librdkafka not thread-safe) → consume loop made **synchronous**, offset stored on the consume thread, `ctx.Ack` no-op. The emulated-DLQ test is `Skip`'d (native librdkafka fault with multiple in-process consumers on macOS — a test-harness/native-lib issue; verify in Linux CI). Both fixes harden the shared pipeline for every transport.
> - ⏳ **NEXT:** verify the Kafka DLQ test on Linux CI; then ASB/NATS + CAP (publish+outbox) behind the same port; per-type routing + consumer groups; Postgres SKIP-LOCKED claim (task `task_7ec0a681`).

---

## 0. The ask (verbatim intent)

1. Analyze **all** event/messaging providers → integrate the most important & related ones.
2. Build the **components for event messaging** this session.
3. Create **all queue infrastructure** — dead-letter, retry, etc.
4. Ship a **saga pattern with declarative construction**: `msg X → X-handler → result to Y queue → Y-handler → result back to X → X-handler`.

The saga line is the headline. It is a **bounded, linear, orchestrated routing chain with compensation** — best served by a **routing-slip / pipeline builder**, not a state machine (which is the escalation path for branching/timeouts).

---

## 1. Recommended model (the answer)

One clean-room **MIT abstraction**, three layers, provider-swappable behind ports:

- **Contract layer (transport-agnostic):** `IMessage` (+ optional `ICommand`/`IEvent` intent markers), `IMessageHandler<TMessage>`, `IMessageBus` (`PublishAsync` fan-out / `SendAsync` point-to-point), and a rich `MessageContext<T>` that **auto-propagates correlation / conversation / causation ids** and exposes `Publish/Send/Reply` (the single best ergonomic, stolen from MassTransit's `ConsumeContext`). Handlers are POCO classes discovered by DI scan — **identical mental model to the existing in-process mediator**, so a product graduates in-proc → broker with no handler rewrite.
- **Reliability layer (ports):** `IRetryPolicy`+`RetryConfig`, `IDeadLetterStore` (with **replay**), `IMessageScheduler` (delay), `IInboxStore` (idempotency/dedupe), `IOutbox`/`IOutboxDispatcher` (transactional publish). The SDK **owns an emulation** for DLQ + retry-with-backoff because the two most likely first targets (Kafka, RabbitMQ) lack one or both natively.
- **Transport layer (adapters):** an in-box **in-memory transport** (`System.Threading.Channels`) ships as the **zero-broker default** (tests + single-host). Brokers (RabbitMQ, Kafka, ASB, NATS) and the **CAP outbox** plug in behind `ISendTransport`/`IReceiveTransport` + capability flags as **opt-in adapter packages** — never core dependencies.

**Declarative saga** = a **routing-slip / pipeline builder** (clean-room of MassTransit Courier's *itinerary-as-data* + automatic reverse-compensation). The itinerary is data → it works over **both** the in-process mediator and a broker via one `ISagaTransport` seam, and exports to Mermaid for free visualization. State-machine (Stateless-backed) and message-cascading (Wolverine-style `return next`) are the two later escalation styles, sharing the same step/context core.

**License spine (load-bearing):** core = **permissive only** (MIT/Apache/BSD). The whole abstraction is clean-room. **MassTransit v9 (commercial, Apr-2025 announcement) and NServiceBus (RPL-1.5 copyleft + paid) are studied, never depended on** — adapter-only, opt-in. **CAP (MIT)** is the blessed distributed-outbox backend; **Wolverine/Brighter/Rebus (all MIT)** and **Stateless (Apache-2.0)** are safe but we mirror their ideas rather than vendor them into core.

---

## 2. Current state — the gap

| | Today |
|---|---|
| `src/Messaging/` | **empty** (this doc fills it) |
| `wow-two-platform.comms.infra` | bare `Backbone.Comms.Infra.sln`, no source |
| In-process dispatch | `Mediator` (`IRequest`/`INotification`/`IPipelineBehavior`) — **in-proc only, no delivery guarantee, no retry/DLQ/saga** |
| Jobs | Hangfire (`src/Jobs/`) — `targets.md` wants "outbox-as-job wired when messaging is enabled" |
| App consumers | **none** — greenfield, nothing to migrate or break |
| Package versions | CAP 8.3.2 (+ PG/SqlServer/Mongo/RabbitMQ/Kafka/ASB), `RabbitMQ.Client` 7, `Confluent.Kafka` 2.6, `Azure.Messaging.ServiceBus` 7.18, `NATS.Client.Core` 2.5, `MQTTnet` 5 — **declared in `Directory.Packages.props`, referenced by nothing** (so adding the abstraction costs zero transitive weight) |

**Relationship to the Mediator:** the mediator is the *in-process* request/notification dispatcher (sync, same call stack). The messaging layer is the *asynchronous, at-least-once, possibly-cross-process* sibling. They deliberately share handler ergonomics but differ in guarantee. The messaging layer's in-memory transport routes through DI scopes like the mediator does, but adds the envelope, retry, DLQ, and scheduling the mediator has no concept of.

---

## 3. External landscape

### 3.1 Framework verdicts (license is the gate)

| Framework | Saga / orchestration | Outbox/Inbox | License | Core-dep? | Verdict |
|---|---|---|---|---|---|
| **MassTransit** | ✅✅ state machine DSL **+ Courier routing-slip** (gold standard) | ✅ EF transactional + inbox dedupe | v8 **Apache-2.0** (EOL ~EOY 2026) · **v9 commercial** (Massient, announced Apr 2025) | **No** | **Adapter-only**; *study the DSL + Courier* |
| **NServiceBus** | ✅✅ imperative sagas + ServiceControl replay UI | ✅ first-class outbox | **RPL-1.5** (copyleft) + paid | **No** | **Adapter-only**; steal the *operational replay* idea |
| **Wolverine** | ✅ low-ceremony sagas + **message cascading** (`return next`) | ✅✅ durable inbox/outbox (headline) | **MIT** | Yes (license) | Mirror ideas; *don't vendor* (huge surface, codegen, DB-coupled) |
| **Brighter** | ◐ hand-built on outbox+correlation | ✅ `DepositPost`/`ClearOutbox` (clean seam) | **MIT** (brief's "BSD" is stale) | Yes | Mirror the *explicit outbox seam* + `[UsePolicy]` Polly attribute |
| **Rebus** | ✅ `Saga<T>` + declarative `CorrelateMessages` | ◐ SQL outbox | **MIT** | Yes | **Best architectural reference** — modular bus/transport/persistence split |
| **DotNetCore.CAP** | ◐ none (weak saga story) | ✅✅ outbox+inbox primary, 7 brokers | **MIT** | **Yes** | **Blessed distributed backend** behind `IOutbox`/`IMessagePublisher` |
| **Stateless** | n/a (state machine only, no transport) | n/a | **Apache-2.0** | Yes | Optional backing for the *state-machine* saga style (later) |

### 3.2 Broker capability matrix (drives the transport-port + emulation design)

✅ native · 🟡 partial/conditional · ❌ none (SDK emulates or omits). All client packages permissive (RabbitMQ.Client Apache/MPL, Confluent.Kafka Apache + librdkafka BSD-2, Azure.Messaging.ServiceBus MIT, NATS.Net Apache, MQTTnet MIT, AWS/GCP SDKs Apache).

| Broker | Native DLQ | Native delay/schedule | Ordering | Native dedupe | Sessions/FIFO | Producer tx |
|---|---|---|---|---|---|---|
| **RabbitMQ** | ✅ DLX→DLQ | 🟡 plugin / TTL+DLX trick | 🟡 single-consumer FIFO | ❌ | 🟡 consistent-hash | 🟡 weak (confirms preferred) |
| **Kafka** | ❌ (dead-letter *topic*) | ❌ (retry topics) | ✅ per-partition | 🟡 idempotent producer (retries only) | 🟡 per-key | ✅ EOS (`transactional.id`) |
| **Azure Service Bus** | ✅ `$DeadLetterQueue` | ✅ scheduled + deferred | ✅ per-session | ✅ by `MessageId` | ✅ sessions + state | 🟡 single-namespace |
| **NATS / JetStream** | ❌ (advisory → DLQ stream) | ✅ `NakWithDelay` | ✅ per-subject | ✅ `Nats-Msg-Id` + window | 🟡 work-queue | ❌ |
| **AWS SQS/SNS** | ✅ redrive policy | ✅ `DelaySeconds` (≤15min) | 🟡 FIFO only | 🟡 FIFO only (5min) | 🟡 FIFO groups | ❌ |
| **GCP Pub/Sub** | ✅ dead-letter topic | 🟡 backoff only | 🟡 ordering key | 🟡 via exactly-once | ❌ | ❌ |
| **In-memory (SDK default)** | 🟡 emulated (`IDeadLetterStore`) | 🟡 emulated (timer+heap) | ✅ single-reader FIFO | 🟡 emulated (`IInboxStore`) | 🟡 per-key channel | ❌ (use outbox) |

**Consequence:** **DLQ + retry-with-backoff are the two capabilities the SDK must own an emulation for** — model dead-letter as an adapter-implemented callback (native sub-queue *or* SDK-managed republish) and retry/backoff as an SDK-side policy that drives `Nack(delay)` where native, else a republish-to-delay loop. At-least-once is the contract floor everywhere → **consumer idempotency (`IInboxStore`) is mandatory, not optional.**

### 3.3 Patterns

- **In-memory bus** = `System.Threading.Channels` (in-box, MIT, async-native, backpressure via bounded channels). Single bounded channel + dispatcher (envelope-typed) as default; per-type channels opt-in. **DI scope per message** (like a web request). DLQ/delay/retry all emulable in-proc.
- **Outbox** solves the dual-write problem (commit state + outbox row in one DB tx; background dispatcher drains → at-least-once). **Inbox** = per-`MessageId` dedupe table → exactly-once *effect*. CAP gives both batteries-included; a hand-rolled EF `SaveChanges`-interceptor outbox is the zero-extra-infra fallback.
- **Resilience** = build on **Polly** (`Microsoft.Extensions.Resilience` is already version-pinned, MIT): retry (exp+jitter), circuit breaker, timeout, rate-limiter in one pipeline — behind `IRetryPolicy` so it never leaks. Hand-roll only the bits Polly doesn't own (retry budget, inbox, DLQ store, scheduler heap). *(The shipped in-memory default hand-rolls exp+jitter to keep core dependency-free; the Polly-backed policy is the first reliability adapter.)*
- **Observability** = OTel messaging semconv: PRODUCER span on publish injecting W3C `traceparent` into `MessageEnvelope.Headers`; CONSUMER span on process extracting it (span link for batches). `messaging.system` / `messaging.operation.type` / `messaging.destination.name` attributes. MassTransit + CAP both do header-based propagation — confirms the pattern.
- **Aspire** owns connection wiring + broker-client config + health checks; the SDK owns the `IMessageBus` port + envelope + reliability. Adapters consume the Aspire-registered client; don't double-register OTel.

### 3.4 Saga / routing-slip (the headline)

- **Pattern fit:** the user's chain is **orchestration** (central-ish coordinator), and structurally a **routing slip** — the itinerary *is* the flow definition (data, not code), and the runtime auto-compensates completed steps in reverse on failure. This is the closest map to "chain of handlers, result passed along, with rollback."
- **MassTransit Courier** (study-only): `IActivity<TArgs,TLog>` (`Execute`+`Compensate`) vs `IExecuteActivity<TArgs>` (no compensation); itinerary + variables + logs travel in the message; `RoutingSlipBuilder.AddActivity/AddVariable`; the **Saga Execution Coordinator** walks compensation logs in reverse automatically.
- **MassTransit state machine** (study-only): `Initially(When(e).Then(...).Publish(...).TransitionTo(s))` / `During` / `Request`/`Response` — the cleanest declarative spelling of "send to Y, get result back to X." The escalation when branching/timeouts appear.
- **Wolverine cascading** (MIT): handlers `return` the next message → framework publishes it; `Respond.ToSender(...)` = "back to X." Purest expression of the chain, but the flow is implicit (no single declarative artifact, no auto-compensation).

---

## 4. Design specifics

### 4.1 Contract shapes (illustrative — shipped under `src/Messaging/Contracts/`)

```csharp
public interface IMessage;                              // optional base marker
public interface ICommand : IMessage;                   // one logical owner (Send)
public interface IEvent : IMessage;                     // fan-out (Publish)

public interface IMessageHandler<in TMessage> where TMessage : class
{
    ValueTask HandleAsync(MessageContext<TMessage> context, CancellationToken cancellationToken);
}

public interface IMessageBus
{
    ValueTask PublishAsync<TMessage>(TMessage message, PublishOptions? options = null, CancellationToken ct = default) where TMessage : class;
    ValueTask SendAsync<TMessage>(string destination, TMessage message, SendOptions? options = null, CancellationToken ct = default) where TMessage : class;
}
```

`MessageEnvelope` carries `MessageId`, `CorrelationId`, `ConversationId`, `CausationId`, `Headers` (W3C trace-context lives here), `DeliveryCount` (poison detection), `NotBeforeUtc` (scheduled), `PartitionKey` (ordering). `MessageContext<T>` wraps `Message` + the envelope + `PublishAsync/SendAsync/ReplyAsync` that **auto-propagate** `ConversationId`/`CausationId` so chains stay correlated without ceremony.

### 4.2 Reliability ports (`src/Messaging/Reliability/`)

```csharp
public enum BackoffKind { None, Fixed, Exponential, ExponentialJitter }
public sealed record RetryConfig(int MaxAttempts = 5, BackoffKind Backoff = BackoffKind.ExponentialJitter,
    TimeSpan? BaseDelay = null, TimeSpan? MaxDelay = null);
public interface IRetryPolicy { TimeSpan? NextDelay(int attempt, RetryConfig config); }

public interface IDeadLetterStore {                     // poison terminus + REPLAY
    ValueTask DeadLetterAsync(DeadLetterRecord record, CancellationToken ct);
    IAsyncEnumerable<DeadLetterRecord> ReadAsync(string source, CancellationToken ct);
    ValueTask ReplayAsync(string messageId, CancellationToken ct);   // redrive, reset DeliveryCount
}
public interface IInboxStore { ValueTask<bool> TryBeginAsync(string messageId, CancellationToken ct); } // false ⇒ duplicate
public interface IMessageScheduler { ValueTask ScheduleAsync(MessageEnvelope envelope, DateTimeOffset notBeforeUtc, CancellationToken ct); }
public interface IOutbox { ValueTask EnqueueAsync(OutboxRecord record, CancellationToken ct); }          // EF/CAP adapters fill these
public interface IOutboxDispatcher { ValueTask<int> DispatchAsync(int batchSize, CancellationToken ct); }
```

### 4.3 Declarative saga API (`src/Messaging/Saga/`) — the routing-slip builder

Clean-room of Courier's concept, our own names. Expresses the user's exact chain top-to-bottom, with per-step compensation and automatic reverse-rollback:

```csharp
public interface ISagaStep
{
    string Name { get; }
    ValueTask<SagaStepOutcome> ExecuteAsync(SagaContext context, CancellationToken ct);
    ValueTask CompensateAsync(SagaContext context, CancellationToken ct);   // default no-op
}

var saga = SagaBuilder.Named("order-fulfilment")
    .Step<ReserveStockStep>()          // message X handled by X-handler → result in SagaContext
    .Step<ChargePaymentStep>()         // result "sent to" payment step (Y) → Y-handler
    .Step<ConfirmOrderStep>()          // result "back to" order step (X-final)
    .Build();

await runner.RunAsync(saga, new SagaContext(seed), ct);
// ChargePaymentStep faults → runner pops the compensation stack:
//   ReserveStockStep.CompensateAsync runs (release stock), in reverse order. SEC = the runner's stack.
```

- The itinerary is **data** → `saga.ToMermaid()` for a free flow diagram (Stateless idea, ported).
- `SagaContext` is the shared variable bag + correlation id; each step reads predecessors' outputs and writes its own — this *is* "result passed along."
- `ISagaTransport` (in-proc default routes through the mediator; broker impl routes through an adapter) makes the **same builder** run in-process *and* distributed — the dual-mode requirement.
- **Escalation (later, same core):** `(b)` a Stateless-backed state-machine style (`When/Then/Send/TransitionTo`) for branching/timeouts; `(c)` Wolverine-style message-cascading sugar (`return next`).

### 4.4 Transport port (adapters, future)

```csharp
public interface ISendTransport { ValueTask SendAsync(MessageEnvelope envelope, CancellationToken ct); }
public interface IReceiveTransport { /* subscribe → ReceiveContext{ Ack, Nack(requeue, delay?), DeadLetter(reason), DeliveryCount } */ }
public interface ITransportCapabilities { bool NativeDeadLetter { get; } bool NativeDelay { get; } bool NativeDedupe { get; } /* … */ }
```

Capability flags let the dispatch pipeline decide native-vs-emulated per broker. RabbitMQ adapter sets `NativeDeadLetter=true`; Kafka adapter sets it `false` and the SDK republishes to a `{topic}.dlq` topic.

---

## 5. Naming

| Concept | Name | Note |
|---|---|---|
| Base message marker | `IMessage` | optional; POCO also allowed |
| Intent markers | `ICommand` / `IEvent` | Send vs Publish (NSB-style semantic typing, MT-style optional) |
| Handler | `IMessageHandler<TMessage>` | matches `targets.md §6` "`IMessageHandler<T>` / `IConsumer<T>` — match canonical name once messaging ships" → **`IMessageHandler<T>` is canonical**; `IConsumer<T>` reserved for the MassTransit adapter's bridge |
| Bus | `IMessageBus` | `PublishAsync` / `SendAsync` |
| Context | `MessageContext<T>` | clone of `ConsumeContext<T>` ergonomics |
| Envelope | `MessageEnvelope` | headers + correlation + delivery-count + schedule |
| Registration | `AddInMemoryMessaging(...)` / `AddMessageHandlersFromAssemblies(...)` / `AddSaga<T>()` | no `WoWTwo` prefix (house rule) |
| Saga step | `ISagaStep` | `Execute` + `Compensate` |
| Saga builder | `SagaBuilder` | fluent, itinerary-as-data |
| DB schema (when persisted) | `wow_two_messaging.*` (outbox/inbox), snake_case | per `naming.md §DB` |
| `ActivitySource` | `WoW.Two.Messaging` | brand-prefixed diagnostics (house rule) |

---

## 6. Component inventory + when

| Component | Action | When |
|---|---|---|
| Contracts: `IMessage`/`ICommand`/`IEvent`, `IMessageHandler<T>`, `IMessageBus`, `MessageContext<T>`, `MessageEnvelope`, `PublishOptions`/`SendOptions` | new (clean-room) | **NOW (built)** |
| Reliability ports: `IRetryPolicy`+`RetryConfig`+`BackoffKind`, `IDeadLetterStore`+`DeadLetterRecord`, `IInboxStore`, `IMessageScheduler`, `IOutbox`/`IOutboxDispatcher`+`OutboxRecord` | new | **NOW (built)** |
| In-memory transport: bus + `Channels` + `MessageConsumerHostedService` (DI-scope-per-message, retry→DLQ) + in-mem `DeadLetterStore`/`InboxStore`/`Scheduler` + `DefaultRetryPolicy` (exp+jitter) | new | **NOW (built)** |
| Declarative saga: `ISagaStep`, `SagaContext`, `SagaStepOutcome`, `SagaBuilder`, `SagaDefinition`, `ISagaRunner`/`SagaRunner` (compensation stack), `ISagaTransport` (in-proc) | new | **NOW (built)** |
| DI: `MessagingServiceCollectionExtensions` (`AddInMemoryMessaging`, handler scan, `AddSaga`) | new | **NOW (built)** |
| Docs: `Messaging.md` / `Messaging.spec.md` / `Messaging.standard.md` | new | **NOW (built)** |
| Polly-backed `IRetryPolicy` + circuit-breaker hook | adapter | NEXT |
| OTel: PRODUCER/CONSUMER spans + `traceparent` inject/extract on envelope | wire to existing OTel | NEXT |
| CAP outbox adapter (`Messaging.Cap`) behind `IOutbox`/`IMessagePublisher` + dashboard | adapter pkg | NEXT |
| EF `SaveChanges`-interceptor outbox + inbox tables (`wow_two_messaging`) | adapter | NEXT |
| RabbitMQ adapter (DLX/DLQ native, confirms, prefetch) | adapter pkg | NEXT |
| Health checks pre-wire (Xabaril RabbitMQ/Kafka/ASB) when a transport is enabled | wire | NEXT |
| Kafka / Azure Service Bus / NATS adapters (capability flags + emulated DLQ where absent) | adapter pkgs | LATER |
| State-machine saga style (Stateless-backed) + message-cascading sugar | new (same core) | LATER |
| MassTransit / Wolverine adapters (opt-in, license-gated) | adapter pkgs | LATER |
| Routing-slip durability (persist itinerary for crash-recovery) | new | LATER |
| AsyncAPI doc generation (Saunter) | adapter | LATER |

---

## 7. Open decisions

- **D1 (transport-port granularity):** one `ISendTransport`/`IReceiveTransport` pair vs per-capability sub-interfaces. *Rec: one pair + `ITransportCapabilities` flags; emulate the gaps SDK-side.*
- **D2 (handler contract):** `IMessageHandler<T>` (chosen) vs reuse the mediator's `INotificationHandler<T>`. *Rec: separate type — messaging adds envelope/retry/DLQ semantics the mediator handler can't express; bridge later if wanted.*
- **D3 (saga first-style):** routing-slip builder (chosen) vs state-machine vs cascading. *Rec: ship routing-slip first (matches the ask, auto-compensation, dual-transport); add the other two over the same step/context core.*
- **D4 (outbox default):** CAP vs hand-rolled EF. *Rec: both behind `IOutbox`; CAP for multi-broker distributed, hand-rolled EF for zero-extra-infra single-broker.*
- **D5 (resilience engine):** Polly vs hand-roll. *Rec: Polly behind `IRetryPolicy` (already pinned, MIT); in-memory default hand-rolls exp+jitter to keep core dep-free.*
- **D6 (exactly-once):** transport-level (fiction) vs effectively-once via inbox. *Rec: at-least-once contract floor + `IInboxStore` for effectively-once; document it loudly.*
- **D7 (doc home):** lives in `docs/planning/messaging/`; on maturity distill into `Messaging.standard.md` + `targets.md §3.5`.

---

## 8. Sources

Primary (verified by the 6 analysis agents, 2026-06-22): masstransit.io / masstransit.massient.com (consumers, state machine, Courier routing-slip, retry/outbox) · massient.com/license + github.com/MassTransit LICENSE (v8 Apache-2.0 → v9 commercial) · docs.particular.net + github.com/Particular/NServiceBus LICENSE (sagas, recoverability, RPL-1.5) · wolverinefx.net (cascading, sagas, durable inbox/outbox; MIT) · brightercommand.gitbook.io + github.com/BrighterCommand (command processor, outbox, `[UsePolicy]`; MIT) · github.com/rebus-org/Rebus wiki (sagas, `CorrelateMessages`, retries; MIT) · cap.dotnetcore.xyz + github.com/dotnetcore/CAP (outbox/inbox, brokers, retry, dashboard; MIT, v8.3) · github.com/dotnet-state-machine/stateless (Apache-2.0, 5.20) · broker docs + client LICENSEs (RabbitMQ.Client, Confluent.Kafka/librdkafka, Azure.Messaging.ServiceBus, NATS.Net, MQTTnet, AWSSDK.SQS, Google.Cloud.PubSub.V1) · opentelemetry.io messaging-spans semconv · nuget.org Microsoft.Extensions.Resilience / Polly.Core (MIT) · learn.microsoft.com Aspire messaging integrations. Internal: `targets.md §2.23/§3.5/§6`, `ideas.md §3.3`, `errors-architecture-investigation.md` (format), `naming.md`, `package-layout.md`.
