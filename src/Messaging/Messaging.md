# Messaging

Provider-agnostic event messaging + declarative sagas. Clean-room MIT abstraction; the **in-memory transport is the
zero-broker default** (works in tests + single-host). Broker adapters (RabbitMQ / Kafka / ASB / NATS) and the CAP outbox
plug in behind the same ports — opt-in, never core deps. Design rationale + provider analysis:
[`docs/planning/messaging/messaging-architecture-investigation.md`](../../docs/planning/messaging/messaging-architecture-investigation.md).

## Layout

| Folder | What |
|---|---|
| `MessagingContracts.cs` | `IMessage`/`ICommand`/`IEvent`, `IMessageHandler<T>`, `IMessageBus`, `MessageContext<T>`, `MessageEnvelope`, `Publish/SendOptions` |
| `Reliability/` | `IRetryPolicy`+`RetryConfig`, `IDeadLetterStore` (+replay), `IInboxStore`, `IMessageScheduler`, `IOutbox`/`IOutboxDispatcher`, `DefaultRetryPolicy` |
| `InMemory/` | Channel-backed bus + `MessageConsumerHostedService` (DI-scope-per-message, retry→DLQ) + in-mem stores |
| `Saga/` | `ISagaStep`/`SagaStep`, `SagaContext`, `SagaBuilder`, `SagaDefinition`, `ISagaRunner`/`SagaRunner`, `ISagaTransport` |

## Pub/sub + handlers

```csharp
builder.Services.AddInMemoryMessaging(typeof(Program).Assembly);   // scans for IMessageHandler<T>

public sealed record OrderPlaced(Guid OrderId) : IEvent;

public sealed class EmailReceiptHandler(IEmailSender email) : IMessageHandler<OrderPlaced>
{
    public async ValueTask HandleAsync(MessageContext<OrderPlaced> ctx, CancellationToken ct)
    {
        await email.SendAsync(/* … */, ct);
        await ctx.PublishAsync(new ReceiptSent(ctx.Message.OrderId), ct);  // correlation auto-propagated
    }
}

await bus.PublishAsync(new OrderPlaced(id));            // fan-out
await bus.SendAsync("orders", new ShipOrder(id));       // point-to-point
```

A handler that throws is retried per `InMemoryMessagingOptions.Retry` (exp+jitter default); on exhaustion the message is
dead-lettered to `IDeadLetterStore` (replayable via `ReplayAsync`). Duplicate message ids are skipped via `IInboxStore`.

## Declarative saga — the routing-slip chain

The headline: `X → X-handler → result to Y → Y-handler → result back to X → X-handler`, with automatic reverse-compensation.

```csharp
var fulfilment = SagaBuilder.Named("order-fulfilment")
    .Step<ReserveStockStep>()     // X        → writes result into SagaContext
    .Step<ChargePaymentStep>()    //   → Y    → reads stock result, writes payment result
    .Step<ConfirmOrderStep>()     //   → back to X (final)
    .Build();

builder.Services.AddSaga(fulfilment);

// run it
var result = await runner.RunAsync(fulfilment, new SagaContext(seed: orderId));
// ChargePaymentStep faults → ReserveStockStep.CompensateAsync runs (release stock), in reverse.
```

```csharp
public sealed class ReserveStockStep(IInventory inv) : SagaStep
{
    public override async ValueTask<SagaStepOutcome> ExecuteAsync(SagaContext ctx, CancellationToken ct)
    {
        var ok = await inv.ReserveAsync((Guid)ctx.Seed!, ct);
        if (!ok) return SagaStepOutcome.Faulted("out of stock");
        ctx.Set("reservation", ok);
        return SagaStepOutcome.Completed();
    }
    public override ValueTask CompensateAsync(SagaContext ctx, CancellationToken ct) => inv.ReleaseAsync((Guid)ctx.Seed!, ct);
}
```

`fulfilment.ToMermaid()` renders the itinerary as a `flowchart` for docs. The itinerary is **data**, so the same definition
runs in-process (default) or over a broker via an `ISagaTransport` adapter.

## See also

- [Messaging.spec.md](./Messaging.spec.md) — full API surface · [Messaging.standard.md](./Messaging.standard.md) — RFC 2119 contract
- Architecture + provider analysis: [`messaging-architecture-investigation.md`](../../docs/planning/messaging/messaging-architecture-investigation.md)
- Sibling in-process dispatcher: [`Mediator/`](../Mediator/mediator.md) (sync, no delivery guarantee)
