# Messaging.RabbitMq

First broker adapter behind the transport port (`Messaging/Transport/`). Publishes to a topic exchange and consumes a
queue with **native DLX/DLQ**; reuses the shared `EventProcessingPipeline` (dedupe → resilience → dispatch → settle),
`IEventHandler<T>`, `IInboxProcessor`, sagas — nothing handler-side changes vs the in-memory transport.

> Compile-verified; **not yet runtime-verified** against a live broker. RabbitMQ.Client 7.x (async API).

## Wire-up

```csharp
builder.Services.AddRabbitMqEventBus(o =>
{
    o.ConnectionString = "amqp://guest:guest@localhost:5672/";
    o.Exchange = "wt.events";      // topic
    o.Queue    = "orders-service"; // this service's queue
}, typeof(Program).Assembly);      // scans for IEventHandler<T>

// same handler contract as in-memory:
public sealed class OrderPlacedHandler : IEventHandler<OrderPlaced>
{
    public ValueTask HandleAsync(EventContext<OrderPlaced> ctx, CancellationToken ct) { /* … */ }
}
```

## Topology (declared on start)

```
publish → exchange "wt.events" (topic, routing key = destination)
                     │  bind "#"
                     ▼
              queue "{Queue}"  ──(x-dead-letter-exchange)──► "wt.events.dlx" (fanout) ──► "wt.events.dlq"
```
- **Publish**: `TransportEventBus` → `RabbitMqSendTransport` → `BasicPublishAsync` (JSON body, `wt-event-type` header = assembly-qualified type, `traceparent` propagated, `Persistent` = `Durable`).
- **Consume**: `RabbitMqReceiveTransport` (prefetch, manual ack) → reconstruct envelope → `EventProcessingPipeline`.
- **Ack / DLQ**: pipeline → `ctx.AcknowledgeAsync` = `BasicAck`; `ctx.DeadLetterAsync` (retries exhausted) = `BasicNack(requeue:false)` → **native DLX → DLQ**. Unparseable message → nacked to DLQ.

## Capabilities

`RabbitMqCapabilities`: `NativeDeadLetter = true` · `NativeDelay/NativeDedupe/NativeOrdering = false` (delay/dedupe emulated by the SDK where needed; single-queue only).

## Types

| Type | Role |
|---|---|
| `RabbitMqOptions` | connection · exchange · queue · DLX/DLQ · prefetch |
| `RabbitMqConnection` | lazy shared `IConnection` (singleton) |
| `RabbitMqSendTransport` | `ISendTransport` — publish to the topic exchange |
| `RabbitMqReceiveTransport` | `IReceiveTransport` — declare topology + consume → pipeline |
| `RabbitMqReceiveContext` | `ReceiveContext` — ack / nack→DLX |
| `AddRabbitMqEventBus(…)` | DI registration |

## NEXT

- **Runtime verify** against a Testcontainers RabbitMQ (publish → consume → DLQ round-trip).
- Per-event-type routing/binding (currently binds `#` = all); consumer-group semantics; connection-recovery tuning.

## See also

- Port: [`../Transport/TransportContracts.cs`](../Transport/TransportContracts.cs) · pipeline: [`../Transport/EventProcessingPipeline.cs`](../Transport/EventProcessingPipeline.cs)
- [`../Messaging.md`](../Messaging.md) · analysis: [`messaging-architecture-investigation.md`](../../docs/planning/messaging/messaging-architecture-investigation.md)
