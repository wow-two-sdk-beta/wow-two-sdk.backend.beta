# Messaging.Kafka

Second broker adapter behind the transport port (`Messaging/Transport/`) — and the one that exercises the
**emulated-DLQ** path (Kafka has no native dead-letter facility). Same `IEventHandler<T>` contract as in-memory /
RabbitMQ; only the wiring differs.

## Why it's the interesting one

`ITransportCapabilities.NativeDeadLetter = false` → the SDK **emulates** dead-lettering: `RabbitMqReceiveContext`
used the broker's DLX; `KafkaReceiveContext.DeadLetterAsync` instead **re-produces the message to a dead-letter topic**
then advances the offset. This proves the port's capability-flag design — the pipeline is identical; only the
`ReceiveContext.DeadLetterAsync` implementation changes per transport.

## Wire-up

```csharp
builder.Services.AddKafkaEventBus(o =>
{
    o.BootstrapServers = "localhost:9092";
    o.Topic           = "wt.events";
    o.GroupId         = "orders-service";   // consumer group
    o.DeadLetterTopic = "wt.events.dlq";    // emulated DLQ
}, typeof(Program).Assembly);
```

## Model

- **Publish**: `TransportEventBus` → `KafkaSendTransport` → `ProduceAsync` (JSON body, `wt-event-type` + `wt-message-id` + `traceparent` headers, key = `PartitionKey ?? MessageId`).
- **Consume**: `KafkaReceiveTransport` — `Subscribe(Topic)`, poll loop (`Consume(500ms)`), reconstruct envelope → `EventProcessingPipeline`. `EnableAutoCommit=true` + `EnableAutoOffsetStore=false` → we `StoreOffset` on settle (commit on ack).
- **Ack**: `ctx.Acknowledge` → `StoreOffset` (offset committed by the auto-committer).
- **DLQ (emulated)**: `ctx.DeadLetter` (retries exhausted) → re-produce the original message to `DeadLetterTopic`, then `StoreOffset` (advance past the poison message).

## Capabilities

`KafkaCapabilities`: `NativeDeadLetter = false` (emulated) · `NativeOrdering = true` (per-partition) · `NativeDelay/NativeDedupe = false`.

## Types

| Type | Role |
|---|---|
| `KafkaOptions` | bootstrap · topic · group · DLQ topic |
| `KafkaSendTransport` | `ISendTransport` — produce to the topic |
| `KafkaReceiveTransport` | `IReceiveTransport` — subscribe + poll → pipeline |
| `KafkaReceiveContext` | `ReceiveContext` — ack = `StoreOffset`; dead-letter = re-produce to DLQ topic |
| `AddKafkaEventBus(…)` | DI registration |

## NEXT

- Per-partition concurrency (currently single poll loop); consumer-group rebalance handling; schema-registry option.

## See also

- Port: [`../Transport/TransportContracts.cs`](../Transport/TransportContracts.cs) · RabbitMQ (native DLQ): [`../RabbitMq/RabbitMq.md`](../RabbitMq/RabbitMq.md)
- [`../Messaging.md`](../Messaging.md) · analysis: [`messaging-architecture-investigation.md`](../../docs/planning/messaging/messaging-architecture-investigation.md)
