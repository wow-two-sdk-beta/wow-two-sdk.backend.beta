# Messaging — session handoff

*Last updated: 2026-07-09 · Status: **green build; 11/12 tests pass (1 skip)** · pick up from NEXT*

> Event-messaging layer for `WoW.Two.Sdk.Backend.Beta` (backend-beta SDK). Greenfield → substantial. Full rationale +
> decision log: [`messaging-architecture-investigation.md`](./messaging-architecture-investigation.md) (read its **Review log** first).
> Repo: `workbench/wow-two-sdk-beta/wow-two-sdk.backend.beta`. Memory: `project_messaging_layer.md`.

## Build & test (do this first to confirm green)

```bash
cd src
export MSBUILDDISABLENODEREUSE=1; ulimit -n 65535
dotnet build WoW.Two.Sdk.Backend.Beta.csproj -m:1                       # mono-lib; expect 0 errors, ~9 allowlisted warns
dotnet test Messaging.Tests/WoW.Two.Sdk.Backend.Beta.Messaging.Tests.csproj   # needs Docker (Testcontainers RabbitMQ+Kafka)
```
- **CS1591**: every *public* type + member needs XML doc (mono-lib, `GenerateDocumentationFile=true`, warnings-as-errors). Positional records → a `<param>` per param.
- **IDE0005** (unused using) errors **only** when `GenerateDocumentationFile=true` (⇒ mono-lib yes, test proj no). Types in an *enclosing* namespace need **no** using (e.g. `…Messaging.Kafka` sees `EventEnvelope` in `…Messaging`).
- Mono-lib globs `src/**`; every `*.Tests/**` is excluded in `DefaultItemExcludes` (`WoW.Two.Sdk.Backend.Beta.csproj:34`) — **add new test folders there**.
- Git: agents never `commit`/`push` (hook-enforced); the dev commits.

## The model (3 layers, one port)

```
IEventBus (TransportEventBus)  →  ISendTransport  →  {in-memory channel | RabbitMQ | Kafka}
                                                         │
handler ← EventProcessingPipeline ← ReceiveContext ← IReceiveTransport  (TransportConsumerHostedService drives it)
          (dedupe → resilience → dispatch → ctx.Ack / ctx.DeadLetter)
```
- **Bus carries `IEvent` only** (constraint `where TEvent : class, IEvent`). Commands/queries stay on the in-process `Mediator`; merge deferred (services use the bus, mustn't inject the mediator).
- **Contract = role, never topology.** No `IIntegrationEvent`. Transport hints (`Durable`/`Priority`/`TimeToLive`/`PartitionKey`) are abstract → adapters map or ignore.
- **Capability flags** (`ITransportCapabilities`) drive native-vs-emulated: RabbitMQ native DLX; **Kafka `NativeDeadLetter=false` → emulates DLQ by re-producing to a dead-letter topic**.

## File map (all under `src/`)

| Area | Path | Key types |
|---|---|---|
| Contracts | `Messaging/MessagingContracts.cs` | `IEvent`, `IEventHandler<T>`, `IEventBus`, `EventContext<T>`, `EventEnvelope`, `Publish/SendOptions` |
| Transport port | `Messaging/Transport/` | `ISendTransport`, `IReceiveTransport`, `ReceiveContext`, `ITransportCapabilities`, `EventProcessingPipeline`, `TransportConsumerHostedService`, `TransportEventBus`, `EventDispatcher`/`Registry` |
| Reliability | `Messaging/Reliability/MessagingReliability.cs` + `EventResilience.cs` | `IRetryPolicy`/`RetryConfig`/`BackoffKind`/`DefaultRetryPolicy`, `IEventResiliencePipeline`, `IDeadLetterStore`/`DeadLetterRecord`, `IInboxProcessor`, `IEventScheduler`, `IOutbox`/`OutboxRecord`/`IOutboxDispatcher` |
| Reliability · Polly | `Messaging/Reliability/Polly/` | `AddPollyEventResilience(…)` (retry+breaker+timeout) |
| Reliability · EF | `Messaging/Reliability/Ef/` | `EfOutbox<T>`, `OutboxDispatcher<T>` + `IOutboxClaimStrategy`, `EfInboxProcessor<T>`, entities, `Ef.md` (migration SQL) |
| In-memory | `Messaging/InMemory/` | channel, `InMemorySend/ReceiveTransport`, `InMemoryReceiveContext`, `InMemoryInboxProcessor`, `InMemoryDeadLetterStore`, `InMemoryEventScheduler`, `DefaultEventResiliencePipeline`, options |
| Event saga | `Messaging/EventSaga/` | `EventSagaBuilder`, `IEventSagaStep`/`EventSagaStep`, `EventSagaRunner`, `EventSagaContext`, `IEventSagaTransport` |
| RabbitMQ | `Messaging/RabbitMq/` (`RabbitMq.md`) | `RabbitMqSend/ReceiveTransport`, `RabbitMqReceiveContext` (native DLX), `RabbitMqCapabilities`, `AddRabbitMqEventBus` |
| Kafka | `Messaging/Kafka/` (`Kafka.md`) | `KafkaSend/ReceiveTransport`, `KafkaReceiveContext` (emulated DLQ), `KafkaCapabilities`, `AddKafkaEventBus` |
| DI | `Messaging/MessagingServiceCollectionExtensions.cs` | see Registration |
| Diagnostics | `Messaging/MessagingDiagnostics.cs` | `WoW.Two.Messaging` `ActivitySource` (OTel; wired via `AddOpenTelemetryTracing` → `AddSource("WoW.Two.*")`) |
| Docs | `Messaging/Messaging.{md,spec.md,standard.md}` | folder lead + API spec + RFC-2119 contract |
| Persistence CQRS | `Data/Abstractions/IWriteRepository.cs`, `Data/CqrsRepositoryServiceCollectionExtensions.cs`, `Data/…/Repositories/` | `IWriteRepository`, `AddCqrsRepository<TEntity,TId,TContext>`, `AddEfWriteRepositories`, `AddDapperReadRepository` |
| Pluggable EF interceptors | `Data/EntityFrameworkCore/Interceptors/` | `AddEfSaveChangesInterceptor<T>()` (auto-wired by `AddEntityFrameworkCore`) |
| Tests | `Messaging.Tests/` | in-memory + RabbitMQ + Kafka (Testcontainers) |

## Registration (composable)

```csharp
services.AddInMemoryEventBus([o => o.Retry = …], asm);        // = handlers + reliability + transport (batteries-included)
services.AddRabbitMqEventBus(o => { o.ConnectionString=…; o.Queue=…; }, asm);
services.AddKafkaEventBus(o => { o.BootstrapServers=…; o.Topic=…; o.GroupId=…; o.DeadLetterTopic=…; }, asm);
services.AddEventSaga(EventSagaBuilder.Named("x").Step<A>().Step<B>().Build());
// granular: AddInMemoryEventTransport / AddInMemoryReliability / AddEventResilienceDefaults / AddEventHandlersFromAssemblies
// polly:  AddPollyEventResilience(o => o.UseCircuitBreaker = true);
// outbox: db.OnModelCreating → modelBuilder.ApplyOutboxModel().ApplyInboxModel();
//         AddEfOutbox<Db>() + AddEfOutboxDispatcher<Db>() + AddEfInbox<Db>();  (migration SQL in Reliability/Ef/Ef.md)
// repo:   AddCqrsRepository<Product, Guid, Db>();   // reads→Dapper, writes→EF (additive, non-enforcing)
```
Handler contract is identical across every transport: `class H : IEventHandler<TEvent> { ValueTask HandleAsync(EventContext<TEvent>, CancellationToken); }`.

## Verified (tests, Docker)

- ✅ in-memory: round-trip · dedupe (`IInboxProcessor`) · dead-letter (retry-exhausted → `IDeadLetterStore`).
- ✅ RabbitMQ: round-trip · **native DLX/DLQ** over a real broker.
- ✅ Kafka: round-trip (publish→consume→handle).
- ⏭ Kafka DLQ: `Skip`'d — native librdkafka crash with multiple in-process consumers on **macOS**; unskip + verify on Linux CI.

## Gotchas (learned the hard way)

- **Kafka / librdkafka is NOT thread-safe.** The consume loop is **synchronous** (`onMessage(...).GetAwaiter().GetResult()`), `StoreOffset` runs on the consume thread, `ctx.Ack` is a no-op (offset advanced by the loop). `TransportConsumerHostedService.StopAsync` calls `base.StopAsync` (drain loop) **before** `receiveTransport.StopAsync` (dispose) — reversing it closes the native consumer mid-`Consume` → crash. Don't "simplify" either.
- **CAP is a framework, not a transport** — peer to this layer (over a transport + DB). It plugs UNDER our port later; don't invert it. Its `[CapSubscribe]` is compile-time (no catch-all) — the consume bridge is the hard part.
- Options configured via `Action<T>` use **mutable** `{ get; set; }` (records with `init` break `Configure`). Retry config lives on `InMemoryEventBusOptions.Retry`.

## NEXT (pick up here)

1. **Kafka DLQ on Linux CI** — unskip `KafkaEventBusTests.Dead_letters_failing_message_to_dlq_topic`; should pass off-macOS. (NATS DLQ already passes on macOS — managed client, no librdkafka crash.)
2. **Next broker behind the port** — **CAP** (publish + its outbox/dashboard; consume-bridge is the fork) or **ASB** (native DLQ, clean; rounds out the matrix — RabbitMQ-native · Kafka+NATS-emulated). Mirror `Kafka/` or `Nats/`.
3. **Per-type routing/binding + consumer groups** (RabbitMQ binds `#`, Kafka/NATS one subject today).
4. **Webhooks +1** — durable subscription store + management API + delivery/DLQ persistence (`Webhooks/Webhooks.spec.md` §Future).
5. **Repo split enforcement** (user's call — currently additive/non-enforcing).

## Shipped 2026-07-09 (3-lane parallel batch — agents + lead)

- **NATS JetStream adapter** (`Nats/`) — 3rd broker; emulated DLQ (re-publish to dead-letter subject) like Kafka but **settles in-context like RabbitMQ** (NATS.Net managed/thread-safe → no librdkafka sync-loop hack). Auto-provisions stream + durable consumer (`FilterSubject`=events subject). `AddNatsEventBus`; `NATS.Client.JetStream 2.5.5`. Round-trip + **DLQ tests green on macOS**. Test gotcha: shell-less `nats` image → Testcontainers `UntilMessageIsLogged("Server is ready")`, **not** an exec/port-probe (which hangs `StartAsync` forever).
- **PG SKIP-LOCKED outbox claim** (`Reliability/Ef/PostgresSkipLockedOutboxClaimStrategy.cs`) — `SELECT … FOR UPDATE SKIP LOCKED`, tx bridged onto `DbContext.SavedChanges` for exactly-once across instances; `ReplaceWithPostgresSkipLockedOutboxClaim<TContext>()`. Stateless singleton; real-Postgres 2-dispatcher exactly-once test. (closes `task_7ec0a681`)
- **Webhooks** (`Webhooks/`) — outbound HMAC-SHA256-signed (`timestamp.body`, Stripe-style) delivery via `IHttpClientFactory`, transient-only retry, in-memory subscription store, `IWebhookDeliveryLog` seam. `AddWebhooks`. P4.

## Done this session (don't redo)

event-centric rename · transport-abstract options · `EventSaga` routing-slip · pluggable EF interceptors · EF outbox (stage + dispatcher + `IOutboxClaimStrategy` seam) · exactly-once `EfInboxProcessor` (same-tx) · read/write repo split (non-enforcing) · Polly resilience seam · OTel producer/consumer spans (traceparent on headers) · transport port + in-memory re-seat · RabbitMQ adapter · Kafka adapter · test suite.
