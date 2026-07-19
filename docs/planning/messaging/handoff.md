# Events layer — session handoff

*Last updated: 2026-07-19 · Continue the events-layer maturity build in `wow-two-sdk.backend.beta`.*

> **What to build (source of truth):** [`events-maturity-backlog.md`](./events-maturity-backlog.md) — 5-agent gap analysis vs MassTransit/NServiceBus/Wolverine/Rebus/Brighter/CAP/Dapr; 3 tiers (A correctness · B keystone seams · C breadth) + a **Deferred** table. This file = current state + how to continue.
> Repo: `workbench/wow-two-sdk-beta/wow-two-sdk.backend.beta`. A fresh chat auto-recalls memory `project_messaging_layer`.

## Start a new chat with

> "continue the backend-beta events layer — read `docs/planning/messaging/events-maturity-backlog.md` + `handoff.md`; next seam = **<pick from NEXT>**."

## Build & test (do first to confirm green)

```bash
cd src; export MSBUILDDISABLENODEREUSE=1; ulimit -n 65535
dotnet build WoW.Two.Sdk.Backend.Beta.csproj -m:1                                    # 0 errors; 41 allowlisted NU/CA warns (all NuGet advisories, none in Messaging)
dotnet test Messaging.Tests/WoW.Two.Sdk.Backend.Beta.Messaging.Tests.csproj -m:1     # needs Docker (Rabbit/Kafka/NATS/PG). 29 pass / 1 skip (Kafka DLQ = macOS librdkafka multi-consumer crash; verify on Linux CI)
```
- **Traps** (warnings-as-errors): CS1591 (XML doc every *public* member), IDE0005 (no unused usings — e.g. drop `System.Text.Json` after adopting `IMessageSerializer`). Test project (beta-forever exception) = `Messaging.Tests/`; **CA2263** → use AwesomeAssertions generic `Be<T>()` for `Type` asserts.
- **`JsonOptionsPresets`**: keep `MakeReadOnly(populateMissingResolver: true)` — reverting crashes in reflection-JSON-off contexts.
- Git: agents never `commit`/`push`; the dev commits.

## Shipped this session (green, pushed)

**Wave 0 — correctness (backlog §1):**
- A1 Kafka/NATS unparseable → re-produce to DLQ (`wt-dl-reason`), not silent ack-drop.
- A2 delivery-count wired — NATS `Metadata.NumDelivered` · RabbitMQ `Redelivered` · Kafka baseline `1` (true count deferred).
- A5 outbox `MaxDispatchAttempts` give-up + `PruneProcessedAsync` retention sweep.
- A6 `ReceiveContext.DeadLetterAsync(reason, Exception?, ct)` captures exception type; Kafka/NATS `wt-dl-*` death headers.
- A9 `AddOpenTelemetryMetrics` → `AddMeter("WoW.Two.*")` (SDK meters were uncollected).

**Wave 1 — keystone seams (backlog §2):**
- **Serializer + type-resolver** (`Messaging/Serialization/MessageSerialization.cs`): `IMessageSerializer` (default STJ via `JsonOptionsPresets`) + `IMessageTypeResolver` / `MessageTypeRegistry` / `DefaultMessageTypeResolver` — **stable `FullName` token** from the handler + `IEvent`-contract scan, AQN fallback, aliases → kills the AQN→`Type.GetType` silent-drop. `EventEnvelope.ContentType` + `wt-content-type` header. Threaded RabbitMQ/Kafka/NATS (in-memory doesn't serialize). Seams: `AddMessageSerializer<T>` / `AddMessageTypeResolver<T>` / `MapMessageType<T>(token)`.
- **Consume-filter pipeline** (`Transport/ConsumeFilter.cs`): `IConsumeFilter` + `ConsumeDelegate` + `AddConsumeFilter<T>()`. `EventProcessingPipeline` is now an ordered filter chain wrapping the resilience→dedupe→dispatch core (empty = behavior-preserving; first-registered = outermost). Unblocks fault-publish / wire-tap / claim-check / rate-limit / per-consumer breaker.

## Shipped 2026-07-19 — sequence items 1–3 (suite 24 pass / 1 skip)

- **Concurrency pump** (`Transport/MessagePump.cs`) — `ConcurrencyOptions` + N workers + bounded per-worker channel (`FullMode.Wait` = backpressure). Default `MaxConcurrentMessages = 1` dispatches inline, behaviour-preserving like the empty filter chain. Per-key ordering routes `PartitionKey` through a stable FNV-1a hash to one worker. `InFlight` counter feeds the drain, the metrics gauge, and (next) `IBusControl`. `AddMessagingConcurrency(...)` is the seam. 4 tests: sequential default · parallel speedup · per-key order · drain-on-stop.
- **`wt-partition-key`** — the key was previously lost on RabbitMQ and NATS entirely (never stamped, never reconstructed), so per-key ordering would have silently degraded to unordered on 2 of 3 brokers. Kafka keeps the native `Key`.
- **`ITransportCapabilities` → 13 flags** — added `SettlesInContext` · `ThreadAffineConsume` (both read by the pump) plus `NativePriority` · `NativeTimeToLive` · `NativeScheduling` · `NativeSessions` · `NativePublisherConfirms` · `NativeTransactions` · `NativeRequestReply`. Implemented honestly on all 4 adapters — a conditional capability reports `false` with the reason in a comment.
- **A7 partial** — RabbitMQ now maps `Priority` (clamped `[0,255]`) and `TimeToLive` → `Expiration` ms. Kafka/NATS have no per-message equivalent and now *say so* via the flags instead of dropping the hint silently.
- **`IMessagingMetrics`** (`MessagingMetrics.cs`) — meter `WoW.Two.Sdk.Messaging`, collected by the existing `AddMeter("WoW.Two.*")`. Sent/consumed counters, process-duration histogram, dead-lettered (tagged `error.type` only), retried, in-flight observable gauge. `NoOpMessagingMetrics` opts out. Never tags message/correlation/partition id.
- **Outbox on the serializer seam** — `OutboxDispatcher` moved off direct STJ + `Type.GetType` onto `IMessageSerializer` / `IMessageTypeResolver`; rows carry `content_type` and a mismatch now fails the row instead of feeding it mismatched bytes. **Schema:** `outbox_messages` needs `content_type varchar(100) NOT NULL DEFAULT 'application/json'`.
- **`wt-` is a reserved header namespace** (`MessageHeaders.IsReserved`) — caller headers previously overwrote every control header, so forwarding a received envelope's `Headers` on a re-publish stamped the *previous* message's type token onto the new body. Control headers now write last, on all 3 brokers.
- **Kafka key no longer fabricated** — `Key = envelope.PartitionKey ?? envelope.MessageId` meant a consumer got back a `PartitionKey` nobody chose, and a unique key per message defeats the sticky partitioner. Now null when unset.

## Shipped 2026-07-19 (second batch) — Tier-B complete + A3/A4

- **`IBusControl`** (`Transport/BusControl.cs`) — `BusState` (Stopped/Running/Paused/Draining) · `Pause`/`Resume`/`Stop` · `InFlight`. Pause is an `AsyncGate` at the top of `MessagePump.DispatchAsync`, upstream of both pump modes: a paused bus makes the consume loop *wait*, so messages stay unacked at the broker and prefetch is the backpressure. Open path is one `Volatile.Read` returning a completed `ValueTask` — no allocation when nobody pauses. Gate sits before the in-flight increment, so an admitted message always completes.
- **Observers** (`Transport/MessageObservers.cs`) — `IPublishObserver` · `IReceiveObserver` · `IConsumeObserver` + `AddMessageObserver<T>()`. Distinct from `IConsumeFilter`: a filter wraps and may short-circuit, an observer only watches and cannot change settlement. Every hook is per-observer try/caught (`EventId 6041`); zero registered observers costs a `.Length` check, no async state machine.
- **Exception classification** (`Reliability/EventFaultClassification.cs`) — `FaultDisposition { Retry, DeadLetter, Ignore }` + `IEventFaultClassifier`, configured by type (`DeadLetterOn<T>` / `IgnoreOn<T>` / `RetryOn<T>`) or predicate. Fixes "every exception burns `MaxAttempts`". Needed **zero** `Transport/` changes: a `DeadLetter` verdict propagates into the pipeline's existing catch, `Ignore` returns normally onto the ack path. Applied to both the Polly and default pipelines so behaviour can't diverge by which is registered; `Retry` is the zero value, so unconfigured = today's behaviour.
- **A3 connection recovery** — the receiver's `Task.Delay(Timeout.Infinite)` is now a supervisor loop on `ChannelShutdownAsync` + consumer `UnregisteredAsync` **plus** a 10s `IsOpen`/`IsRunning` poll, which is the only thing that catches A3's actual failure: recovery reopens the connection but never re-subscribes, raising no event at all. `GetConnectionAsync` no longer gates on `IsOpen` — with recovery on, closed means "reconnecting", and the old check opened a second connection per blip.
- **A4 publisher confirms** — channel opened with `CreateChannelOptions(publisherConfirmationsEnabled, publisherConfirmationTrackingEnabled)`; in 7.x `BasicPublishAsync` itself completes only on broker ack and throws `PublishException` on nack, so awaiting the publish *is* the confirm wait. No retry loop (would double-publish). **`SendAsync` now costs a broker round-trip and surfaces failures that were silently swallowed.**
- 5 more tests: non-retryable dead-letters immediately · ignored acks · unclassified still retries · pause holds then resume releases · stop drains to zero in-flight.

## NEXT — agreed sequence (work top-down, backlog §2)

1. **Topology + per-type routing** ← *start here* — replaces the RabbitMQ `#` catch-all; unblocks per-endpoint DLQ + subscriptions. Also the prerequisite for RabbitMQ priority actually *ranking* (needs `x-max-priority` at declare — see traps).
2. **`IRequestClient`** — request/response; needs `ReplyTo` on the envelope + temp-queue topology (1). `NativeRequestReply` is already true for RabbitMQ + NATS.
3. **State-machine saga + `ISagaRepository`** (epic) — correlate-by-key, timeouts, durability.
4. **Consumer-facing test harness** (§3.12) — now unblocked by the observer seam; every test still re-rolls `EventCollector`.
5. **A8 re-enqueue delayed retry** — in-proc `Task.Delay` still holds the message unsettled through the whole backoff.

Tier-B is complete. Then Tier-C breadth (§3), starting with adapter breadth (ASB → SQS → Redis Streams).

### Open follow-ups from this batch

- **Kafka pause evicts from the group** — pause parks the dedicated consume thread, so a long pause risks `max.poll.interval.ms`. Real fix is native `Consumer.Pause(partitions)` behind an `ITransportCapabilities` branch.
- **`Ignore` records no consumed metric** — the `RecordConsumed` call sits inside the action lambda that threw. Needs a `ConsumeOutcome.Ignored` plus a `Transport/` call site.
- **`Messaging.spec.md:69` understates the retry contract** now that classification exists.
- RabbitMQ.Client 7.0.0 skews publish seq numbers on concurrent publish failure (fixed in 7.1.x) — reason to bump the pin.

### Traps learned building the pump

- **Drain must not run on the transport's stopping token.** `base.StopAsync` cancels it before draining, so handing it to the handler abandons exactly the in-flight work the drain exists to finish. Workers run on the pump's own token, cancelled only at dispose.
- `TransportConsumerHostedService.StopAsync` order is load-bearing and now three-step: stop the loop → `pump.DrainAsync` → release the transport. Releasing earlier disposes the channel a worker is about to ack on.
- **Kafka is thread-affine** — `Consume`/`StoreOffset` must stay on the consume thread. `ThreadAffineConsume = true` makes the pump degrade to sequential and log it. Parallelising Kafka needs offsets marshalled back to the loop; don't "simplify" the flag away.
- **RabbitMQ priority rides the wire but is not ranked** — ranking needs `x-max-priority` on the queue, and adding it to a live queue fails redeclare with `PRECONDITION_FAILED`. Needs the topology work (item 2) plus a migration, not a code tweak.
- `PrefetchCount` must be ≥ `MaxConcurrentMessages` or it becomes the effective concurrency limit.
- **Webhooks deliberately did NOT adopt the serializer seam** — `JsonOptionsPresets` is camelCase/null-omitting where the current path is `JsonSerializerOptions.Default`, so every signed byte and every property name would change and break existing subscribers. Needs an opt-in switch or a v2 signature scheme.
- **`EventId` 6401/6402/6403 collide** between `Kafka/KafkaTransport.cs` and `Webhooks/WebhookPublisher.cs` — pre-existing, unrenumbered.

## Deferred — analysis preserved (backlog "Deferred" table)

A3/A4 conn auto-recovery + publisher confirms · A7/A8 broker delay/TTL/priority mapping + re-enqueue retry (**Wave 2**) · Kafka true delivery-count · outbox/webhooks serializer adoption · MessagePack/Protobuf/Avro/CloudEvents serializers · **per-consumer inbox scoping** (inbox is `message_id`-only; mature = composite `(consumer_id, message_id)` — backlog §3.6).

## Other

- **Shelved: `identity/core`** — own sliced-lego user model, step 1 of a 10-step epic ([`identity/identity-architecture.md`](../identity/identity-architecture.md); memory `project_identity_rebuild`). SEPARATE batch — check git for whether it's committed.
- **Parallelization that worked**: lead builds the seam core + one reference adapter; disjoint adapter files (Kafka/NATS) fan out to agents with the reference pattern + a preserve-existing-logic guard.
- Full architecture/decision log: [`messaging-architecture-investigation.md`](./messaging-architecture-investigation.md).
