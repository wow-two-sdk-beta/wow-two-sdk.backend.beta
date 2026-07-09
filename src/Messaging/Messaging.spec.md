# Messaging — spec

*Last updated: 2026-07-08*

## NuGet

```
WoW.Two.Sdk.Backend.Beta  (mono-lib; namespace WoW.Two.Sdk.Backend.Beta.Messaging[.*])
```

## Public API

### Contracts (`WoW.Two.Sdk.Backend.Beta.Messaging`)

| Type | Notes |
|---|---|
| `IEvent` | Marker for a bus event. **The bus accepts `IEvent` only** — commands/queries stay on the mediator. Topology (in-mem vs broker) and scope (local vs cross-service) are absent from the contract by design. |
| `IEventHandler<TEvent>` | `ValueTask HandleAsync(EventContext<TEvent>, CancellationToken)`; `where TEvent : class, IEvent`. Many per event type. |
| `IEventBus` | `PublishAsync<T>(T, PublishOptions?, ct)` (fan-out) · `SendAsync<T>(string destination, T, SendOptions?, ct)` (point-to-point). Both `where T : class, IEvent`. |
| `EventContext<TEvent>` | `Event` · `Envelope` · `MessageId` · `CorrelationId` · `ConversationId` · `Headers` · `PublishAsync`/`SendAsync` (auto-propagate correlation + set `CausationId`). |
| `EventEnvelope` | `record`: `MessageId`, `Body`, `BodyType`, `Destination`, `CorrelationId`, `ConversationId`, `CausationId`, `DeliveryCount`, `NotBeforeUtc`, `PartitionKey`, `Durable`, `Priority`, `TimeToLive`, `Headers`. |
| `PublishOptions` / `SendOptions` | `MessageId`, `CorrelationId`, `ConversationId`, `CausationId`, `Delay`, `PartitionKey`, `Durable`, `Priority`, `TimeToLive`, `Headers`. **Transport-abstract** — adapters map to native primitives or ignore. |

### Reliability (`…Messaging.Reliability`)

| Type | Notes |
|---|---|
| `BackoffKind` · `RetryConfig(MaxAttempts=5, Backoff=ExponentialJitter, BaseDelay?, MaxDelay?)` · `IRetryPolicy` · `DefaultRetryPolicy` | Retry schedule. `NextDelay` returns `null` → dead-letter. |
| `IDeadLetterStore` | `DeadLetterAsync` · `ReadAsync(source)` · `ReplayAsync(messageId)` (redrive). `DeadLetterRecord.From(env, ex, nowUtc)`. |
| `IInboxProcessor.ProcessOnceAsync(messageId, handler)` | Exactly-once seam: dedupe + (EF) run handler in the **same transaction**. `false` ⇒ duplicate (skip). |
| `IEventScheduler.ScheduleAsync(envelope, notBeforeUtc)` | Delayed delivery. |
| `IOutbox` / `OutboxRecord` / `IOutboxDispatcher` | Transactional-outbox ports. |

### EF reliability (`…Messaging.Reliability.Ef`)

| Type | Notes |
|---|---|
| `OutboxMessageEntity` · `OutboxModelBuilderExtensions.ApplyOutboxModel()` | Maps `outbox_messages`. |
| `EfOutbox<TContext>` · `AddEfOutbox<TContext>()` | `IOutbox` — direct-adds a row to `TContext` (atomic on `SaveChanges`). |
| `OutboxDispatcherOptions` · `AddEfOutboxDispatcher<TContext>()` | Polls pending → deserialize by `type` → publish to `IEventBus` → stamp `ProcessedOnUtc`. |
| `IOutboxClaimStrategy` · `PollingOutboxClaimStrategy` | Pending-row claim seam. Default polls (single-instance); a Postgres `FOR UPDATE SKIP LOCKED` impl plugs in for multi-instance scale-out. |
| `InboxMessageEntity` · `ApplyInboxModel()` · `AddEfInbox<TContext>()` | `IInboxProcessor` — inbox row + handler in **one transaction** (true exactly-once). |

Migration SQL + wire-up: [`Reliability/Ef/Ef.md`](./Reliability/Ef/Ef.md).

### In-memory transport (`…Messaging.InMemory`)

| Type | Notes |
|---|---|
| `InMemoryEventBusOptions` | `ChannelCapacity` (default 1024; 0 = unbounded) · `Retry` (`RetryConfig`). |
| *(internal)* `InMemoryEventBus`, `EventConsumerHostedService`, `InMemory{DeadLetterStore,InboxStore,EventScheduler}` | Registered by `AddInMemoryEventBus`. |

### Event saga (`…Messaging.EventSaga`)

| Type | Notes |
|---|---|
| `IEventSagaStep` / `EventSagaStep` | `Name` · `ExecuteAsync(EventSagaContext, ct) → EventSagaStepOutcome` · `CompensateAsync`. |
| `EventSagaStepOutcome` | `Completed()` / `Faulted(reason)`. |
| `EventSagaContext` | `CorrelationId` · `Seed` · `Items` · `Set` · `Get<T>`. |
| `EventSagaBuilder` | `Named(name)` → `.Step<T>()` / `.Step(Type)` → `.Build()`. |
| `EventSagaDefinition` | `Name` · `StepTypes` · `ToMermaid()`. |
| `IEventSagaRunner.RunAsync(definition, context, ct)` → `EventSagaResult` | Compensates completed steps in reverse on failure. |
| `IEventSagaTransport.SendAsync<TEvent>(destination, event, context, ct)` | Step-emitted event seam (`where TEvent : class, IEvent`). |

### Resilience (`…Messaging.Reliability` · `.Polly`)

| Type | Notes |
|---|---|
| `IEventResiliencePipeline.ExecuteAsync(action, ct)` | Owns the retry loop; throws when exhausted → consumer dead-letters. Default = `IRetryPolicy`-backed. |
| `PollyEventResilienceOptions` · `AddPollyEventResilience(…)` | Swap in a Polly pipeline (exp+jitter retry + optional circuit breaker + per-attempt timeout). |

### Registration (`…Messaging`) — composable

| Method | Notes |
|---|---|
| `AddInMemoryEventBus([Action?], params Assembly[])` | Batteries-included = handlers + reliability + transport. |
| `AddInMemoryEventTransport(Action?)` | Channel + `IEventBus` + consumer only. |
| `AddInMemoryReliability()` | In-mem `IRetryPolicy`/`IEventResiliencePipeline`/`IInboxStore`/`IDeadLetterStore`/`IEventScheduler`. |
| `AddEventHandlersFromAssemblies(params Assembly[])` | Scan + register handlers (incremental; callable repeatedly). |
| `AddEventSaga(EventSagaDefinition)` | Runner + in-proc transport + definition + step types. |

### Observability

`WoW.Two.Messaging` `ActivitySource` — PRODUCER span on publish/send (injects `traceparent` into `EventEnvelope.Headers`), CONSUMER span on process (extracts it). Auto-collected by `AddOpenTelemetryTracing` via the `WoW.Two.*` wildcard source.

## Quick start — the user's chain (X → Y → back to X)

```csharp
builder.Services.AddInMemoryEventBus(typeof(Program).Assembly);

var saga = EventSagaBuilder.Named("checkout")
    .Step<ProcessOrderStep>()      // event X → X-handler, result → EventSagaContext
    .Step<ChargePaymentStep>()     //         → Y → Y-handler, result → EventSagaContext
    .Step<ConfirmOrderStep>()      //         → back to X (final)
    .Build();
builder.Services.AddEventSaga(saga);

var result = await runner.RunAsync(saga, new EventSagaContext(seed: cmd));
// a faulting step → reverse-order CompensateAsync on the completed steps.
```

## Delivery semantics

- **At-least-once** floor → consumers MUST be idempotent; `IInboxStore` gives effectively-once by `MessageId`.
- **Ordering**: single bounded channel = FIFO write order; parallel consumers don't preserve order (use `PartitionKey` — broker adapters map to native ordering).

## See also

- [Messaging.standard.md](./Messaging.standard.md) · [Messaging.md](./Messaging.md)
- [`messaging-architecture-investigation.md`](../../docs/planning/messaging/messaging-architecture-investigation.md)
