# Messaging.Nats

Third broker adapter behind the transport port (`Messaging/Transport/`), on **NATS JetStream**. Same
`IEventHandler<T>` contract as in-memory / RabbitMQ / Kafka — only the wiring differs. Like Kafka it exercises the
**emulated-DLQ** path (JetStream has no native dead-letter facility); unlike Kafka it **settles in the receive
context** (NATS.Net is thread-safe, so no single-thread offset dance is needed).

## Why it's a distinct proof

- **Emulated DLQ, in-context settle.** `ITransportCapabilities.NativeDeadLetter = false` → the SDK emulates
  dead-lettering: `NatsReceiveContext.DeadLetterAsync` **re-publishes the original message to a dead-letter subject**
  then acks the poison message. `AcknowledgeAsync` acks directly (contrast Kafka, whose ack is a no-op because
  `StoreOffset` must run on the consume thread). The pipeline is identical; only settlement differs per transport.
- **JetStream = at-least-once.** Core NATS is fire-and-forget (at-most-once, no ack) — unsuitable for an event bus.
  The adapter provisions a **stream** (persists events + dead-letters) and a **durable consumer** (explicit ack,
  `MaxDeliver`, `FilterSubject` = events subject so it never re-consumes the DLQ subject).

## Wire-up

```csharp
builder.Services.AddNatsEventBus(o =>
{
    o.Url               = "nats://localhost:4222";
    o.Stream            = "wt-events";      // created if absent (subjects: events + dead-letter)
    o.Subject           = "wt.events";
    o.DurableConsumer   = "orders-service"; // durable — survives restarts
    o.DeadLetterSubject = "wt.events.dlq";  // emulated DLQ
    o.MaxDeliver        = 5;                 // broker redelivery cap (crash safety-net; SDK does in-process retries)
}, typeof(Program).Assembly);
```

## Model

- **Publish**: `TransportEventBus` → `NatsSendTransport` → `js.PublishAsync` (JSON body, `wt-event-type` +
  `wt-message-id` + `traceparent` headers). Ensures the stream exists on first send (idempotent).
- **Consume**: `NatsReceiveTransport` provisions stream + durable consumer, then an async `ConsumeAsync<byte[]>`
  loop reconstructs the envelope → `EventProcessingPipeline`.
- **Ack**: `ctx.Acknowledge` → `msg.AckAsync()`.
- **DLQ (emulated)**: `ctx.DeadLetter` (retries exhausted) → re-publish the original to `DeadLetterSubject`, then ack
  the poison message so JetStream stops redelivering it.
- **Crash safety-net**: an unhandled failure leaves the message unacked → JetStream redelivers after `AckWait`
  (capped by `MaxDeliver`); the SDK inbox dedupes the replay.

## Consume-loop faults

The consume loop is `ExecuteAsync`'s body, so a fault it cannot absorb propagates to the host, where
`BackgroundServiceExceptionBehavior` decides the outcome: the .NET default `StopHost` takes the process down; an app
that would rather keep serving sets `Ignore`.

## Destination routing rollout

`NatsOptions.RouteByDestination` is off by default — every message rides `NatsOptions.Subject`. Turning it on changes
which subject a message lands on, widens the stream, and raises the server floor, so it is opt-in:

1. **Consumers first.** Deploy the consumers with `RouteByDestination = true`. A consumer with it on filters `Subject`
   **as well as** every routed subject, so it keeps receiving from publishers that have not been switched yet.
2. **The stream's subject list widens** with `{Subject}.>` — a real change to an already-provisioned stream.
   `NatsTopologyBroker` widens it in place (read-modify-write), leaving retention, max-age and replicas untouched.
3. **nats-server 2.10 or newer** is required: the durable consumer then uses JetStream's multi-subject filter.
4. Once every consumer is across, turn it on for the publishers. A publish lands on the message type's own subject;
   an explicit `SendAsync` lands on the addressed endpoint's subject. Routed subjects nest under the root
   (`wt.events.order-placed`).

## Capabilities

`NatsCapabilities`: `NativeDeadLetter = false` (emulated) · `NativeOrdering = true` (per-subject) ·
`NativeDelay / NativeDedupe = false`.

## Types

| Type | Role |
|---|---|
| `NatsOptions` | url · stream · subject · durable consumer · dead-letter subject · max-deliver |
| `NatsSendTransport` | `ISendTransport` — JetStream publish (ensures stream) |
| `NatsReceiveTransport` | `IReceiveTransport` — provision + async consume loop → pipeline |
| `NatsReceiveContext` | `ReceiveContext` — ack = `AckAsync`; dead-letter = re-publish to DLQ subject + ack |
| `NatsTopology` | idempotent stream + durable-consumer provisioning |
| `AddNatsEventBus(…)` | DI registration |

## NEXT

- Per-subject wildcard routing (one durable per event type) · pull vs push consumer tuning · JetStream-native
  `Nats-Msg-Id` dedupe (`NativeDedupe = true`) · scheduled delivery via JetStream (currently emulated).

## See also

- Port: [`../Transport/TransportContracts.cs`](../Transport/TransportContracts.cs) · emulated-DLQ sibling: [`../Kafka/Kafka.md`](../Kafka/Kafka.md) · native-DLQ sibling: [`../RabbitMq/RabbitMq.md`](../RabbitMq/RabbitMq.md)
- [`../Messaging.md`](../Messaging.md) · analysis: [`engineering/planning/messaging/messaging-architecture-investigation.md`](../../../../../planning/messaging/messaging-architecture-investigation.md)
