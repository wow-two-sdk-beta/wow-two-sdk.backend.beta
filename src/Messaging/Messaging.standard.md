# Messaging — standard

*Last updated: 2026-07-08*

> Behavioral contract (RFC 2119: MUST / SHOULD / MAY). Applies to the abstraction and every transport adapter.

## Contracts

- A bus event **MUST** implement `IEvent`; the bus **MUST** accept `IEvent` only. Commands and queries **MUST** stay on the in-process mediator, not the bus.
- Event contracts **MUST NOT** encode delivery topology (in-process vs broker) or scope (service-local vs cross-service) — both are wiring/routing choices resolved outside the contract.
- `IEventBus.PublishAsync` **MUST** deliver to *every* registered `IEventHandler<T>` for the event type (fan-out). `SendAsync` **MUST** target a single logical destination.
- A handler **MUST** receive an `EventContext<T>` whose `Envelope.MessageId` is stable across redeliveries of the same logical message.
- `EventContext<T>.PublishAsync`/`SendAsync` **MUST** propagate `CorrelationId` (falling back to the current `MessageId`) and **MUST** set the current `MessageId` as the outgoing `CausationId`.
- `PublishOptions`/`SendOptions` transport hints (`Durable`, `Priority`, `TimeToLive`, `PartitionKey`, `Delay`) are **abstract**: an adapter **MUST** map each to its native mechanism or ignore it — never fail because a hint is unsupported.

## Delivery & reliability

- Delivery **MUST** be at-least-once; the abstraction **MUST NOT** claim exactly-once at the transport layer. Consumers **SHOULD** be idempotent; the SDK **MUST** offer `IInboxProcessor` for exactly-once effect by `MessageId`. A durable `IInboxProcessor` **MUST** commit the dedupe mark in the **same transaction** as the handler's effect (both commit or neither).
- A failed handler invocation **MUST** be retried per the resolved `RetryConfig` until `IRetryPolicy.NextDelay` returns `null`, then handed to `IDeadLetterStore`.
- `IRetryPolicy.NextDelay` **MUST** return `null` once `attempt >= MaxAttempts` and **MUST** never exceed `MaxDelay`.
- `OperationCanceledException` from host shutdown **MUST NOT** be treated as a handler failure (no retry, no dead-letter).
- A duplicate `MessageId` (seen by `IInboxProcessor`) **MUST** be acknowledged and skipped.
- `IDeadLetterStore.ReplayAsync` **MUST** re-deliver to the original destination with `DeliveryCount` reset to 0.
- Scheduled delivery (`Delay` / `IEventScheduler`) **MUST NOT** deliver before `NotBeforeUtc`. The in-memory scheduler **MAY** drop scheduled messages on shutdown.

## Transactional outbox / inbox

- `IOutbox.EnqueueAsync` **MUST** enrol the outgoing event in the ambient DB transaction so it commits atomically with the business write (no dual-write, no distributed transaction). The EF impl **MUST** add the row to the caller's context and **MUST NOT** call `SaveChanges` itself.
- The SDK **MUST NOT** require aggregates to implement any domain-event marker; events are staged explicitly.
- `IOutboxDispatcher` **MUST** publish each pending row exactly once on success (stamp `ProcessedOnUtc`) and **MUST** leave it pending (recording the error, bumping `Attempts`) on failure. The DDL for `outbox_messages`/`inbox_messages` is owned by the bespoke migrator; EF maps over it.

## Transport adapters

- An adapter **MUST** preserve handler-facing semantics so a product graduates in-process → broker with no handler change.
- An adapter **MUST** expose capability flags (native DLQ / delay / dedupe / ordering / transactions). Where a capability is not native (e.g. Kafka DLQ), the SDK **MUST** emulate it rather than silently drop it.
- An adapter **SHOULD** propagate W3C trace-context (`traceparent`) via `EventEnvelope.Headers`.
- The core meta-package **MUST NOT** depend on a non-permissive messaging library (MassTransit v9 / NServiceBus → adapter packages only).

## Event sagas

- A saga **MUST** run its steps in declared order; a `Faulted` outcome (or a thrown exception other than shutdown cancellation) **MUST** stop forward progress.
- On failure the runner **MUST** invoke `CompensateAsync` on each previously-completed step in **reverse** order; a throwing compensation **MUST NOT** halt the remaining compensations and **MUST** be logged.
- `EventSagaContext` **MUST** be shared by reference across all steps of one run. `EventSagaResult.Succeeded` **MUST** be `true` only when every step completed.

## Observability

- The bus and consumer **SHOULD** emit OpenTelemetry messaging spans (`PRODUCER` on publish/send, `CONSUMER` on process) via the `WoW.Two.Messaging` `ActivitySource`. A dead-letter write **SHOULD** emit a metric + error log.

## See also

- [Messaging.spec.md](./Messaging.spec.md) · [Messaging.md](./Messaging.md)
