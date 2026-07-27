# Messaging — standard

*Last updated: 2026-07-19*

> Behavioral contract (RFC 2119: MUST / SHOULD / MAY). Applies to the abstraction and every transport adapter.

## Contracts

- A serializer swap **MUST NOT** change type identity: the wire token **MUST** stay in `wt-event-type` and **MUST NOT** be embedded in the body. A serializer that instantiates the CLR type a payload names (MessagePack's typeless resolvers, JSON `TypeNameHandling`) **MUST NOT** be used — it is a remote-code-execution vector on untrusted broker input.
- A bus event **MUST** implement `IEvent`; the bus **MUST** accept `IEvent` only. Commands and queries **MUST** stay on the in-process mediator, not the bus.
- Event contracts **MUST NOT** encode delivery topology (in-process vs broker) or scope (service-local vs cross-service) — both are wiring/routing choices resolved outside the contract.
- `IEventBus.PublishAsync` **MUST** deliver to *every* registered `IEventHandler<T>` for the event type (fan-out). `SendAsync` **MUST** target a single logical destination.
- A handler **MUST** receive an `EventContext<T>` whose `Envelope.MessageId` is stable across redeliveries of the same logical message.
- `EventContext<T>.PublishAsync`/`SendAsync` **MUST** propagate `CorrelationId` (falling back to the current `MessageId`) and **MUST** set the current `MessageId` as the outgoing `CausationId`.
- `PublishOptions`/`SendOptions` transport hints (`Durable`, `Priority`, `TimeToLive`, `PartitionKey`, `Delay`) are **abstract**: an adapter **MUST** map each to its native mechanism or ignore it — never fail because a hint is unsupported.

## Delivery & reliability

- Delivery **MUST** be at-least-once; the abstraction **MUST NOT** claim exactly-once at the transport layer. Consumers **SHOULD** be idempotent; the SDK **MUST** offer `IInboxProcessor` for exactly-once effect by `MessageId`. A durable `IInboxProcessor` **MUST** commit the dedupe mark in the **same transaction** as the handler's effect (both commit or neither).
- Every `IEventResiliencePipeline` **MUST** route a thrown exception through the registered `IEventFaultClassifier` **before spending an attempt**, and **MUST** honour the verdict: `Retry` → retry as configured; `DeadLetter` → propagate at once, spending no attempt, so the consume pipeline dead-letters on the first failure; `Ignore` → swallow and return normally, so the consume pipeline's success path acknowledges. With no rules registered every exception classifies as `Retry`, which **MUST** be behaviourally identical to the pre-classification pipeline.
- A failed handler invocation classified `Retry` **MUST** be retried per the resolved `RetryConfig` until `IRetryPolicy.NextDelay` returns `null`, then handed to `IDeadLetterStore`.
- `IRetryPolicy.NextDelay` **MUST** return `null` once `attempt >= MaxAttempts` and **MUST** never exceed `MaxDelay`.
- Every terminal outcome **MUST** be counted exactly once on `IMessagingMetrics.RecordConsumed`, including an acknowledgement reached via an `Ignore` verdict (`ConsumeOutcome.Ignored`) — a settled message **MUST NOT** leave the pipeline uncounted.
- `OperationCanceledException` from host shutdown **MUST NOT** be treated as a handler failure (no retry, no dead-letter), and **MUST NOT** be classified.
- Under `DelayedRetryOptions.Enabled` an `IEventResiliencePipeline` **MUST** stop after a single attempt and propagate, so the wait happens between deliveries rather than in the loop with the message unsettled. The coordinator **MUST** schedule the next delivery **before** acknowledging the current one (a crash in between is a redelivery, never a loss) and **MUST** bound re-enqueues so a permanently-failing message cannot redeliver forever.
- A duplicate `MessageId` (seen by `IInboxProcessor`) **MUST** be acknowledged and skipped.
- `IDeadLetterStore.ReplayAsync` **MUST** re-deliver to the original destination with `DeliveryCount` reset to 0.
- Scheduled delivery (`Delay` / `IEventScheduler`) **MUST NOT** deliver before `NotBeforeUtc`. The in-memory scheduler **MAY** drop scheduled messages on shutdown.

## Concurrency & runtime control

- The consume pump's default (`MaxConcurrentMessages = 1`) **MUST** dispatch inline and sequentially — identical to the pre-pump path. A transport declaring `ThreadAffineConsume` **MUST** be forced to 1 regardless of configuration.
- With `PreserveKeyOrder` enabled, messages sharing a `PartitionKey` **MUST** be routed to the same worker; messages without one **MAY** be distributed freely. Ordering **MUST NOT** be claimed across different keys.
- The pump **MUST** apply backpressure to the receive loop rather than buffering unboundedly in-process, so unclaimed messages stay at the broker.
- `IBusControl.PauseAsync` **MUST** gate *entry* to the pipeline and **MUST NOT** abandon work already in flight, buffer messages in-process, or settle them at the broker. The gate **MUST** precede the in-flight increment, so a paused message is never counted as in flight.
- `IBusControl` transitions **MUST** be idempotent (pausing a paused bus is a no-op) and a nonsensical transition **MUST** throw `InvalidOperationException` rather than silently succeed. `StopAsync` **MUST** bound its drain by `DrainTimeout` and **MUST NOT** throw on timeout.

## Topology & headers

- Bindings **MUST** be derived from the registered handler set — one routing key per consumed message type. A catch-all binding that delivers everything and filters in-process **MUST NOT** be used.
- A routing key **MUST** be built from `IMessageTypeResolver`'s stable token, never the assembly-qualified name, and **MUST** be sanitized so broker wildcards (`#`, `*`) can never reach it.
- The default `TopologyStyle` **MUST** be `SharedEndpoint`, preserving an existing deployment's queue and dead-letter-queue names; changing endpoint shape **MUST** be opt-in.
- The `wt-` prefix is reserved for SDK control headers. A reserved header **MUST NOT** propagate from a consumed message onto one published while handling it — a control header describes the message it arrived on, so carrying one forward would stamp the previous body's type token or id onto a new body.
- **An adapter MUST strip only the headers it re-derives.** `IsAdapterOwned` is a strict subset of `IsReserved`, and the send path **MUST** filter caller headers on `IsAdapterOwned`, never on `IsReserved`. An adapter **MUST** re-stamp each adapter-owned key from the envelope (so a caller-supplied copy is overwritten and cannot forge the wire contract) and **MUST** carry every other reserved header through untouched. Reserved-but-not-owned keys are stamped by SDK *features* and re-derived by nothing: stripping them leaves the feature working in-memory and silently dead behind every broker. A header added to `IsAdapterOwned` without a matching re-stamp **MUST** be treated as the same defect.
- A reserved header belonging to a feature (`wt-slr-tier`, `wt-dl-redrive-count`, `wt-claim-check*`, `wt-saga-timeout-*`) **MUST** survive a re-publish — retry, delay or dead-letter redrive — or second-level retry, the redrive cap and claim check each break in a way no adapter reports.
- Every reserved header name **MUST** be composed from `MessageHeaders.ReservedPrefix` rather than spelled as a raw `wt-…` literal, so the namespace has one definition and a header cannot drift out of the reserved set by typo.
- Header propagation **MUST** be allow-list based and default to W3C trace context only. Headers set explicitly on the outgoing message **MUST** win over propagated ones.

## Transactional outbox / inbox

- `IOutbox.EnqueueAsync` **MUST** enrol the outgoing event in the ambient DB transaction so it commits atomically with the business write (no dual-write, no distributed transaction). The EF impl **MUST** add the row to the caller's context and **MUST NOT** call `SaveChanges` itself.
- The SDK **MUST NOT** require aggregates to implement any domain-event marker; events are staged explicitly.
- `IOutboxDispatcher` **MUST** publish each pending row exactly once on success (stamp `ProcessedOnUtc`) and **MUST** leave it pending (recording the error, bumping `Attempts`) on failure. The DDL for `outbox_messages`/`inbox_messages` is owned by the bespoke migrator; EF maps over it.

## Transport adapters

- An adapter **MUST** preserve handler-facing semantics so a product graduates in-process → broker with no handler change.
- An adapter **MUST** expose capability flags (native DLQ / delay / dedupe / ordering / transactions). Where a capability is not native (e.g. Kafka DLQ), the SDK **MUST** emulate it rather than silently drop it.
- An adapter **SHOULD** propagate W3C trace-context (`traceparent`) via `EventEnvelope.Headers`.
- An adapter **MUST** obtain the wire body from `EventEnvelope.ToWireBody(serializer)` rather than serializing `Body` directly, and **MUST** stamp `wt-event-type` from `WireBodyType`, not `BodyType`. Serializing directly silently discards every send-path transformation (claim check, compression, encryption); stamping the logical type would name a shape the bytes do not have.
- A send-path transformation **MUST NOT** alter `Body` or `BodyType` — routing, metrics, observers and the outbox read them, and changing either re-routes the message to a key nothing is bound for.
- An adapter **MUST NOT** depend on a package only reachable transitively. A package the adapter compiles against **MUST** carry its own `PackageReference`, so an unrelated dependency's bump or removal cannot break it.
- The core meta-package **MUST NOT** depend on a non-permissive messaging library (MassTransit v9 / NServiceBus → adapter packages only).

## Claim check

- Claim check **MUST** be off unless `AddEventClaimCheck(…)` was called. With it unregistered the send path **MUST** be byte-identical to one where the feature does not exist, and the consume path **MUST** cost no more than a single failed header lookup per message.
- A body at or under `ThresholdBytes` **MUST** travel inline and **MUST NOT** cause a blob write. A body over it **MUST** be written to `IBlobStorage` before the envelope reaches the adapter, and the envelope **MUST** carry `wt-claim-check`, `wt-claim-check-size` and `wt-claim-check-type`.
- An offloaded message **MUST** leave `Body`/`BodyType` as the real contract and substitute only `RawBody`/`RawBodyType`, so routing still resolves from the published type.
- The offloader **MUST NOT** offload a message that already carries a reference — a retry, delay or redrive hop re-publishes the pointer, and offloading again would store a blob whose content is a pointer and hand the handler a `ClaimCheckReference`.
- The rehydrator **MUST** restore the real body before dispatch, and **MUST** leave settlement on the transport's own `ReceiveContext` so acknowledgement and dead-lettering still describe the message that arrived.
- A reference that cannot be honoured **MUST** raise `ClaimCheckPayloadException` **before** the chain continues, so no retry attempt is spent on a fault redelivery cannot fix, and the exception's message **MUST** name the reference and the message id — it becomes the dead-letter reason an operator triages on.
- A reference arriving over the wire is untrusted input. It **MUST** be normalized (rejecting traversal segments) and confined to `PathPrefix`, and a reference failing either check **MUST** be refused **without any call to the blob store**. A blob larger than `MaxPayloadBytes` **MUST** be refused rather than read.
- Consuming, retrying or dead-lettering a claim-checked message **MUST NOT** delete its blob; only the retention sweep may. A dead-letter record **MUST** retain the reference rather than the rehydrated body, so a redrive within `Retention` can still fetch it.
- The retention sweep **MUST NOT** take the host down on failure — unreached blobs are storage cost, not a correctness problem — and **MUST** be disableable for a store with its own lifecycle rules.

## Event sagas

- A saga **MUST** run its steps in declared order; a `Faulted` outcome (or a thrown exception other than shutdown cancellation) **MUST** stop forward progress.
- On failure the runner **MUST** invoke `CompensateAsync` on each previously-completed step in **reverse** order; a throwing compensation **MUST NOT** halt the remaining compensations and **MUST** be logged.
- `EventSagaContext` **MUST** be shared by reference across all steps of one run. `EventSagaResult.Succeeded` **MUST** be `true` only when every step completed.

## Observability

- The bus and consumer **SHOULD** emit OpenTelemetry messaging spans (`PRODUCER` on publish/send, `CONSUMER` on process) via the `WoW.Two.Messaging` `ActivitySource`. A dead-letter write **SHOULD** emit a metric + error log.
- An `IMessagingMetrics` implementation **MUST NOT** throw — every member sits on the publish or consume path, which treats metrics as pure observation and does not guard the call sites. It **MUST NOT** tag with message id, correlation id, or partition key: each is unbounded cardinality. A recorded exception **MUST** contribute its **type** only, never its message.
- The messaging layer **MUST NOT** force a metrics dependency: registration **MUST** be `TryAdd`-based so a consumer's own implementation (or `NoOpMessagingMetrics`) wins.
- An observer (`IPublishObserver` / `IReceiveObserver` / `IConsumeObserver`) **MUST NOT** be able to change settlement, short-circuit the chain, or suppress a fault; an exception it throws **MUST** be caught and logged, never propagated. A registered observer **MUST NOT** change behaviour relative to none being registered. Observers are resolved as singletons and **MUST** be thread-safe.
- `IReceiveObserver` **MUST** be notified once per message (its fault hook after settlement, so the message is at rest); `IConsumeObserver` **MUST** be notified once per delivery attempt.

## See also

- [Messaging.spec.md](./Messaging.spec.md) · [Messaging.md](./Messaging.md)
