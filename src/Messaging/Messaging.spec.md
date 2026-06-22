# Messaging — spec

*Last updated: 2026-06-22*

## NuGet

```
WoW.Two.Sdk.Backend.Beta  (mono-lib; namespace WoW.Two.Sdk.Backend.Beta.Messaging[.*])
```

## Public API

### Contracts (`WoW.Two.Sdk.Backend.Beta.Messaging`)

| Type | Notes |
|---|---|
| `IMessage` | Optional base marker; POCOs also accepted. |
| `ICommand : IMessage` | Intent: single owner → `SendAsync`. |
| `IEvent : IMessage` | Intent: fan-out → `PublishAsync`. |
| `IMessageHandler<TMessage>` | `ValueTask HandleAsync(MessageContext<TMessage>, CancellationToken)`. Many per message type. Invariant `TMessage` (context is invariant). |
| `IMessageBus` | `PublishAsync<T>(T, PublishOptions?, ct)` · `SendAsync<T>(string destination, T, SendOptions?, ct)`. |
| `MessageContext<TMessage>` | `Message` · `Envelope` · `MessageId` · `CorrelationId` · `ConversationId` · `Headers` · `PublishAsync`/`SendAsync` (auto-propagate correlation + set this message as `CausationId`). |
| `MessageEnvelope` | `record`: `MessageId`, `Body`, `BodyType`, `Destination`, `CorrelationId`, `ConversationId`, `CausationId`, `DeliveryCount`, `NotBeforeUtc`, `PartitionKey`, `Headers`. |
| `PublishOptions` / `SendOptions` | `MessageId`, `CorrelationId`, `ConversationId`, `CausationId`, `Delay`, `Headers` (+ `PartitionKey` on send). |

### Reliability (`…Messaging.Reliability`)

| Type | Notes |
|---|---|
| `BackoffKind` | `None` · `Fixed` · `Exponential` · `ExponentialJitter`. |
| `RetryConfig(MaxAttempts=5, Backoff=ExponentialJitter, BaseDelay?, MaxDelay?)` | `BaseDelay` default 200ms, `MaxDelay` default 30s. |
| `IRetryPolicy.NextDelay(attempt, config)` | Returns `TimeSpan?`; `null` = stop (dead-letter). |
| `DefaultRetryPolicy` | Dependency-free exp/jitter impl. |
| `IDeadLetterStore` | `DeadLetterAsync` · `ReadAsync(source)` · `ReplayAsync(messageId)` (redrive, resets `DeliveryCount`). |
| `DeadLetterRecord` | `MessageId, Destination, Reason, ExceptionType?, Envelope, DeadLetteredAtUtc` + `From(env, ex, nowUtc)`. |
| `IInboxStore.TryBeginAsync(messageId)` | `false` ⇒ duplicate (skip). |
| `IMessageScheduler.ScheduleAsync(envelope, notBeforeUtc)` | Delayed delivery. |
| `IOutbox` / `IOutboxDispatcher` / `OutboxRecord` | Transactional-outbox ports (EF / CAP adapters implement; not wired by the in-memory default). |

### In-memory transport (`…Messaging.InMemory`)

| Type | Notes |
|---|---|
| `InMemoryMessagingOptions` | `ChannelCapacity` (default 1024; 0 = unbounded) · `Retry` (`RetryConfig`). |
| *(internal)* `InMemoryMessageBus`, `MessageConsumerHostedService`, `InMemory{DeadLetterStore,InboxStore,MessageScheduler}` | Registered by `AddInMemoryMessaging`. |

### Saga (`…Messaging.Saga`)

| Type | Notes |
|---|---|
| `ISagaStep` | `Name` · `ExecuteAsync(SagaContext, ct) → SagaStepOutcome` · `CompensateAsync(SagaContext, ct)`. |
| `SagaStep` | Abstract base: `Name` = type name, `CompensateAsync` = no-op. |
| `SagaStepOutcome` | `Completed()` / `Faulted(reason)`; `Succeeded`, `FailureReason`. |
| `SagaContext` | `CorrelationId` · `Seed` · `Items` · `Set(key,val)` · `Get<T>(key)`. |
| `SagaBuilder` | `Named(name)` → `.Step<T>()` / `.Step(Type)` → `.Build()`. |
| `SagaDefinition` | `Name` · `StepTypes` · `ToMermaid()`. |
| `ISagaRunner.RunAsync(definition, context, ct)` | → `SagaResult`. Compensates completed steps in reverse on failure. |
| `SagaResult` | `Succeeded`, `FailedStep?`, `FailureReason?`, `CompensatedSteps`. |
| `ISagaTransport.SendAsync<T>(destination, message, context, ct)` | Step-emitted message seam (in-proc default; broker adapter later). |

### Registration (`…Messaging`)

| Method | Notes |
|---|---|
| `AddInMemoryMessaging(params Assembly[])` | Scan given assemblies (default: caller) for handlers; wire bus + reliability + consumer. |
| `AddInMemoryMessaging(Action<InMemoryMessagingOptions>?, params Assembly[])` | As above with options. |
| `AddSaga(SagaDefinition)` | Register the runner, in-proc transport, the definition, and each step type (transient). |

## Quick start — the user's chain (X → Y → back to X)

```csharp
// 1. messaging + handlers
builder.Services.AddInMemoryMessaging(typeof(Program).Assembly);

// 2. declarative saga (routing slip): the linear orchestrated chain with compensation
var saga = SagaBuilder.Named("checkout")
    .Step<ProcessOrderStep>()      // message X → X-handler, result → SagaContext
    .Step<ChargePaymentStep>()     //           → Y queue → Y-handler, result → SagaContext
    .Step<ConfirmOrderStep>()      //           → back to X (final handler)
    .Build();
builder.Services.AddSaga(saga);

// 3. run
var result = await runner.RunAsync(saga, new SagaContext(seed: cmd));
if (!result.Succeeded)
    log.LogWarning("checkout failed at {Step}; compensated {Steps}", result.FailedStep, string.Join(",", result.CompensatedSteps));
```

Each step reads its predecessors' outputs from `SagaContext` (the "result passed along") and writes its own; a faulting step
triggers reverse-order `CompensateAsync` on the completed steps (the Saga Execution Coordinator is the runner's stack).

## Delivery semantics

- **At-least-once** is the contract floor (the in-memory retry path can re-deliver) → consumers MUST be idempotent. `IInboxStore`
  provides effectively-once by deduping on `MessageId`.
- **Ordering**: a single bounded channel with FIFO write order; parallel consumers do not preserve order (use `PartitionKey` +
  per-key serialization when ordering matters — broker adapters map this to native ordering).

## See also

- [Messaging.standard.md](./Messaging.standard.md) · [Messaging.md](./Messaging.md)
- [`messaging-architecture-investigation.md`](../../docs/planning/messaging/messaging-architecture-investigation.md)
