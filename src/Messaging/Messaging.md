# Messaging

Event-driven messaging + declarative event sagas. Clean-room MIT abstraction; the **in-memory transport is the
zero-broker default** (works in tests + single-host). Broker adapters (RabbitMQ / Kafka / ASB / NATS) and the CAP outbox
plug in behind the same ports — opt-in, never core deps. Design + provider analysis + decision log:
[`docs/planning/messaging/messaging-architecture-investigation.md`](../../docs/planning/messaging/messaging-architecture-investigation.md).

> **The bus carries `IEvent` only** — commands/queries stay on the in-process `Mediator`. Delivery topology (in-memory
> vs broker) and scope (service-local vs cross-service) are wiring/routing choices, deliberately absent from the contract.

## Layout

| Folder | What |
|---|---|
| `MessagingContracts.cs` | `IEvent`, `IEventHandler<TEvent>`, `IEventBus`, `EventContext<TEvent>`, `EventEnvelope`, `Publish/SendOptions` (with transport-abstract hints) |
| `Reliability/` | `IRetryPolicy`+`RetryConfig`, `IDeadLetterStore` (+replay), `IInboxStore`, `IEventScheduler`, `IOutbox`/`IOutboxDispatcher`, `DefaultRetryPolicy` |
| `Reliability/Ef/` | EF transactional outbox — `OutboxMessageEntity`, `EfOutbox<TContext>`, `AddEfOutbox<TContext>()`, migration ([`Ef.md`](./Reliability/Ef/Ef.md)) |
| `InMemory/` | Channel-backed bus + `EventConsumerHostedService` (DI-scope-per-event, retry→DLQ) + in-mem stores |
| `EventSaga/` | `EventSagaBuilder`, `IEventSagaStep`/`EventSagaStep`, `EventSagaRunner`, `EventSagaContext`, `IEventSagaTransport` |

## Publish / subscribe

```csharp
builder.Services.AddInMemoryEventBus(typeof(Program).Assembly);   // scans for IEventHandler<T>

public sealed record OrderPlaced(Guid OrderId) : IEvent;

public sealed class EmailReceiptHandler(IEmailSender email) : IEventHandler<OrderPlaced>
{
    public async ValueTask HandleAsync(EventContext<OrderPlaced> ctx, CancellationToken ct)
    {
        await email.SendAsync(/* … */, ct);
        await ctx.PublishAsync(new ReceiptSent(ctx.Event.OrderId), ct);   // correlation auto-propagated
    }
}

await bus.PublishAsync(new OrderPlaced(id));                         // fan-out
await bus.SendAsync("orders", new ShipRequested(id),                // point-to-point + transport hints
    new SendOptions { Durable = true, TimeToLive = TimeSpan.FromMinutes(5) });
```

A handler that throws is retried per `InMemoryEventBusOptions.Retry` (exp+jitter default); on exhaustion the event is
dead-lettered to `IDeadLetterStore` (replayable via `ReplayAsync`). Duplicate message ids are skipped via `IInboxStore`.
`PublishOptions`/`SendOptions` carry **transport-abstract** hints (`Durable`/`Priority`/`TimeToLive`/`PartitionKey`/`Delay`)
that each adapter maps to its native queue/exchange/topic primitives.

## Declarative event saga — the routing-slip chain

`X → X-handler → result to Y → Y-handler → result back to X → X-handler`, with automatic reverse-compensation.

```csharp
var fulfilment = EventSagaBuilder.Named("order-fulfilment")
    .Step<ReserveStockStep>()     // X        → writes result into EventSagaContext
    .Step<ChargePaymentStep>()    //   → Y    → reads stock result, writes payment result
    .Step<ConfirmOrderStep>()     //   → back to X (final)
    .Build();

builder.Services.AddEventSaga(fulfilment);

var result = await runner.RunAsync(fulfilment, new EventSagaContext(seed: orderId));
// ChargePaymentStep faults → ReserveStockStep.CompensateAsync runs (release stock), in reverse.
```

`fulfilment.ToMermaid()` renders the itinerary as a `flowchart`. The itinerary is **data**, so the same definition runs
in-process (default) or over a broker via an `IEventSagaTransport` adapter.

## Transactional outbox

Stage an event in the same DB transaction as the business write (atomic, no dual-write): see [`Reliability/Ef/Ef.md`](./Reliability/Ef/Ef.md).
Pluggable EF interceptors (`AddEfSaveChangesInterceptor<T>()`) live in `Data/EntityFrameworkCore/Interceptors/`.

## See also

- [Messaging.spec.md](./Messaging.spec.md) · [Messaging.standard.md](./Messaging.standard.md)
- Architecture + provider analysis: [`messaging-architecture-investigation.md`](../../docs/planning/messaging/messaging-architecture-investigation.md)
- Sibling in-process dispatcher: [`Mediator/`](../Mediator/mediator.md) (commands + queries, sync, no delivery guarantee)
