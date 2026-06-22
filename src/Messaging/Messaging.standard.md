# Messaging — standard

*Last updated: 2026-06-22*

> Behavioral contract (RFC 2119: MUST / SHOULD / MAY). Applies to the abstraction and every transport adapter.

## Contracts

- A message **MAY** be any reference type; it **MAY** implement `IMessage`/`ICommand`/`IEvent`. Adapters **MUST NOT** require a base type or attribute on the payload.
- `IMessageBus.PublishAsync` **MUST** deliver to *every* registered `IMessageHandler<T>` for the message type (fan-out). `SendAsync` **MUST** target a single logical destination.
- A handler **MUST** receive a `MessageContext<T>` whose `Envelope.MessageId` is stable across redeliveries of the same logical message.
- `MessageContext<T>.PublishAsync`/`SendAsync` **MUST** propagate `CorrelationId` (falling back to the current `MessageId`) and **MUST** set the current `MessageId` as the outgoing `CausationId`.

## Delivery & reliability

- Delivery **MUST** be at-least-once; the abstraction **MUST NOT** claim exactly-once at the transport layer. Consumers **SHOULD** be idempotent; the SDK **MUST** offer `IInboxStore` to achieve effectively-once by `MessageId`.
- A failed handler invocation **MUST** be retried per the resolved `RetryConfig` until `IRetryPolicy.NextDelay` returns `null`, after which the message **MUST** be handed to `IDeadLetterStore`.
- `IRetryPolicy.NextDelay` **MUST** return `null` once `attempt >= MaxAttempts`, and **MUST** never exceed `MaxDelay`.
- `OperationCanceledException` raised by host shutdown **MUST NOT** be treated as a handler failure (no retry, no dead-letter).
- A duplicate `MessageId` (seen by `IInboxStore`) **MUST** be acknowledged and skipped, not reprocessed.
- `IDeadLetterStore.ReplayAsync` **MUST** re-deliver to the original destination with `DeliveryCount` reset to 0.
- Scheduled delivery (`PublishOptions.Delay` / `IMessageScheduler`) **MUST NOT** deliver before `NotBeforeUtc`. The in-memory scheduler **MAY** drop scheduled messages on host shutdown.

## Transport adapters

- An adapter **MUST** preserve handler-facing semantics so a product graduates in-process → broker with no handler change.
- An adapter **MUST** expose capability flags (native DLQ / delay / dedupe / ordering / transactions). Where a capability is not native (e.g. Kafka DLQ), the SDK **MUST** emulate it (e.g. republish to a dead-letter destination) rather than silently drop it.
- An adapter **SHOULD** propagate W3C trace-context (`traceparent`) via `MessageEnvelope.Headers` on send and restore it on receive.
- The core meta-package **MUST NOT** take a dependency on a non-permissive messaging library. MassTransit (v9 commercial) and NServiceBus (RPL-1.5) **MUST** ship as opt-in adapter packages only.

## Sagas

- A saga **MUST** run its steps in declared order. A step that returns `SagaStepOutcome.Faulted` **or** throws (other than shutdown cancellation) **MUST** stop forward progress.
- On failure the runner **MUST** invoke `CompensateAsync` on each previously-completed step in **reverse** order. A compensation that throws **MUST NOT** halt the remaining compensations (best-effort rollback) and **MUST** be logged.
- `SagaContext` **MUST** be shared by reference across all steps of one run (results pass along). A step **MUST NOT** observe another run's context.
- `SagaResult.Succeeded` **MUST** be `true` only when every step completed; otherwise it **MUST** name the failed step and the compensated steps.

## Observability

- The bus and consumer **SHOULD** emit OpenTelemetry messaging spans (`PRODUCER` on publish/send, `CONSUMER` on process) using the `WoW.Two.Messaging` `ActivitySource`, with `messaging.system` / `messaging.operation.type` / `messaging.destination.name` attributes.
- A dead-letter write **SHOULD** emit a metric and an error log.

## See also

- [Messaging.spec.md](./Messaging.spec.md) · [Messaging.md](./Messaging.md)
