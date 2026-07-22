# Events layer — handoff

*Last updated: 2026-07-19*

> Source of truth for *what to build*: [`events-maturity-backlog.md`](./events-maturity-backlog.md). This file = timeline + next batch.
> Repo: `workbench/wow-two-sdk-beta/wow-two-sdk.backend.beta`. Memory: `project_messaging_layer`.

## Restart prompt

> "continue the backend-beta events layer — read `docs/planning/messaging/handoff.md`; run BATCH N."

## Build & test

```bash
cd src; export MSBUILDDISABLENODEREUSE=1; ulimit -n 65535
dotnet build WoW.Two.Sdk.Backend.Beta.csproj -m:1                                    # 0 errors; 41 NuGet-advisory warns
dotnet test Messaging.Tests/WoW.Two.Sdk.Backend.Beta.Messaging.Tests.csproj -m:1     # Docker. 96 pass / 1 skip (Redis + Rabbit + Kafka + NATS + PG containers)
```

- warnings-as-errors: CS1591 (XML doc every **public** member) · IDE0005 (no unused usings) · CA2263 (AwesomeAssertions generic `Be<T>()`)
- `JsonOptionsPresets`: keep `MakeReadOnly(populateMissingResolver: true)`
- 1 skip = `KafkaEventBusTests.Dead_letters_failing_message_to_dlq_topic`, macOS librdkafka multi-consumer crash; verify on Linux CI

## Batch method

1 batch = 3–5 lanes, each owning a **disjoint file set**. Agents never build (obj/bin collide) and never edit `MessagingServiceCollectionExtensions.cs` — they report registration lines, the lead applies them, builds once, runs the suite. Then: update this file → define next batch → start it.

---

## Timeline

**B0 · 2026-07-10 — Wave 0 correctness**
A1 unparseable→DLQ · A2 delivery-count · A5 outbox cap+prune · A6 `DeadLetterAsync(reason, ex, ct)` · A9 `AddMeter("WoW.Two.*")`.

**B1 · 2026-07-10 — Wave 1 seams**
`IMessageSerializer` + `IMessageTypeResolver` (stable `FullName` token, AQN fallback — killed the `Type.GetType` silent drop) · `IConsumeFilter` ordered chain.

**B2 · 2026-07-19 — pump + capabilities + metrics + outbox**
- `MessagePump` — N workers, bounded per-worker channel, `MaxConcurrentMessages=1` default dispatches inline. Per-key ordering via FNV-1a on `PartitionKey`. `AddMessagingConcurrency(...)`.
- `wt-partition-key` — key was lost entirely on RabbitMQ + NATS; ordering would have silently degraded on 2 of 3 brokers.
- capabilities 4 → 13 flags, implemented honestly on all 4 adapters.
- `IMessagingMetrics` — meter `WoW.Two.Sdk.Messaging`; counters, duration histogram, in-flight gauge.
- outbox onto the serializer seam; rows carry `content_type`. **Schema:** `outbox_messages.content_type varchar(100) NOT NULL DEFAULT 'application/json'`.
- `wt-` reserved namespace — caller headers could overwrite control headers, so forwarding a received envelope stamped the old type token onto a new body.
- Kafka key no longer fabricated from `MessageId`.

**B3 · 2026-07-19 — bus control + observers + classification + A3/A4**
- `IBusControl` — pause = async gate at the head of `DispatchAsync`, so messages stay unacked at the broker. Gate precedes the in-flight increment.
- observers (publish/receive/consume) — watch only, cannot change settlement; per-observer try/catch; zero-observer cost is a `.Length` check.
- `FaultDisposition {Retry, DeadLetter, Ignore}` — ends every fault burning `MaxAttempts`. Zero `Transport/` changes: `DeadLetter` falls into the existing catch, `Ignore` lands on the ack path.
- A3 — supervisor loop replaces `Task.Delay(Infinite)`; the 10s poll catches recovery-reconnected-but-never-resubscribed, which raises no event.
- A4 — 7.x confirms are a channel-creation option, so awaiting `BasicPublishAsync` *is* the confirm wait. **`SendAsync` now costs a broker round-trip and throws on nack.**
- 9 tests added (4 pump, 5 classification/bus-control). Committed in 4 commits.

**B4 · 2026-07-19 — topology + typed headers + A8 + test harness**
- `ITopologyProvider` + `IEndpointNameFormatter` (`Transport/Topology.cs`) — the RabbitMQ `#` catch-all is gone; routing key is the resolver's stable token, sanitized so `#`/`*` can never reach a key. `TopologyOptions.Style` defaults to `SharedEndpoint`, so queue/DLQ names are unchanged and nothing strands.
- `x-max-priority` is opt-in via `RabbitMqOptions.MaxPriority`; a pre-existing queue is probed on a **throwaway** channel (a rejected declare closes its channel) and logs rather than crashing. `NativePriority` now reflects the option instead of claiming a ranking the broker discarded.
- typed header model + `IMessageHeaderPropagationPolicy` — allow-list, default `traceparent`+`tracestate`; reserved `wt-` blocked in the builder, so a custom policy can't leak a control header. 6 header names were triplicated across the 3 adapters.
- `ReplyTo` on envelope/options/context, threaded through `TransportEventBus`. Inert until adapters map it — BATCH 5.
- A8 delayed retry (`Reliability/DelayedRetry.cs`) — opt-in `DelayedRetryOptions.Enabled`, default off. Schedules **then** acks (crash between = redelivery, not loss). Attempts ride `DeliveryCount`; three independent stops prevent an infinite redelivery loop. Falls back to in-process delay where the transport can't schedule, logged once at startup.
- `MessagingTestHarness` (`src/Testing.Messaging/`, new companion) — published/consumed/faulted/dead-lettered logs, signal-driven waits, `WaitForIdleAsync`. Built purely on the B3 observer seam, no pipeline hooks. 4 tests ported off `Task.Delay` polling, 6 new. Suite 29 → 35.

**B5 · 2026-07-19 — request/response + saga + DLQ admin + sweep**
- `IRequestClient<TReq,TResp>` (`Transport/RequestClient.cs`) — request carries `ReplyTo` + client-generated `ConversationId`; a short-circuiting `IConsumeFilter` completes the pending request before dispatch, so the requester needs no handler for its own responses. Pending entry registered **before** the send (in-memory can reply before `PublishAsync` returns) and removed in one `finally`. **Reply address defaults to the shared endpoint → single-instance only**; a second instance of the same service steals the reply. Per-instance needs `RequestClientOptions.ReplyAddress`.
- adapters finally map `ReplyTo` **and** `ConversationId` — both were inert since the beginning. RabbitMQ native `BasicProperties.ReplyTo`; Kafka/NATS via `wt-` headers.
- Kafka true delivery-count via `wt-delivery-count`. Honest limit: a record re-consumed because its offset never stored comes back byte-identical, so that redelivery is uncountable from the wire.
- state-machine saga (`Saga/**`) — `ISagaRepository<TState>` + `Initially`/`During`/`When`→`TransitionTo`/`Finalize`, correlate-by-key, timeouts on `IEventScheduler`. **Optimistic** concurrency resolved by reload-and-replay, so activities must be idempotent. `AddSaga` must run **before** `AddMessageTopology` or the saga's events get no binding.
- DLQ admin (`Reliability/DeadLetterAdmin.cs`) — browse/redrive/quarantine/purge. Redrive cap rides `wt-dl-redrive-count` on the **envelope**, not the record: a redriven message that dies again gets a brand-new record, so a record-only counter resets every lap. Second-level retry ships as an `IConsumeFilter`, default off.
- sweep: `ConsumeOutcome.Ignored` (ignored messages recorded no metric at all) · Webhook `EventId` 6401–6403 → 6901–6903 · `Messaging.spec.md` + `Messaging.standard.md` brought current.
- Kafka/NATS now honour `envelope.Destination` — both ignored it, so every `SendAsync` went to one topic/subject and `SendAsync`'s point-to-point contract was never kept there. Opt-in `RouteByDestination`, default off. `CorrelationId` mapped on both.
- 11 tests added (request client, saga, DLQ admin). Suite 35 → 46.
- **Two agents died mid-run** (API errors). The outbox lane edited nothing. The test lane left 2 CA2263 errors, fixed by the lead; its tests all pass.

**B6 · 2026-07-19 — outbox trace fix + 2 adapters + header consolidation**
- **outbox headers fixed** — `BuildInvoker` passed a constant null `PublishOptions`, so every staged message lost its headers. Passing the header through wasn't enough: `TransportEventBus` re-stamps `traceparent` from the ambient `Activity`, which in the dispatcher loop is the dispatcher's. Now starts an `outbox dispatch` activity parented to the staged trace, so the producer span joins the same trace. Also: every dispatch attempt was minting a fresh `MessageId`, making a post-crash redelivery undetectable by the consumer's inbox. No schema change.
- **Azure Service Bus adapter** (`AzureServiceBus/**`) — native DLQ, scheduled enqueue, per-message TTL, sessions from `PartitionKey`, native `ReplyTo`/`CorrelationId`/`DeliveryCount`. The first adapter where most `Native*` flags are genuinely true.
- **Redis Streams adapter** (`RedisStreams/**`) — consumer groups, PEL-backed real delivery-count, `XAUTOCLAIM` stale recovery, `MAXLEN` trimming (default 100k, DLQ untrimmed). Polls rather than blocks: StackExchange.Redis multiplexes one connection, so a blocking `XREADGROUP` would stall every other command.
- **header constants consolidated** — `KafkaHeaders`/`NatsHeaderKeys`/`RabbitMqHeaders` deleted; all 43 sites now use `MessageHeaders`, with `CorrelationId` + `DeliveryCount` lifted in. Every reserved constant is composed from `ReservedPrefix`, so `IsReserved` covers them by construction.
- **Three agents died mid-run** (API/network). Both adapter lanes were near-complete; the lead fixed 1 error each (`Span.TrimEnd` arity, CA1859). Suite unchanged at 46 — **neither new adapter has a single test**.

**B7 · 2026-07-19 — Redis tests + serializers + claim-check + adapter review**
- **Header-strip regression fixed.** B2's reserved-namespace rule stripped every caller `wt-` key on send, but adapters only re-stamp 8 of them. So the headers SDK *features* stamp — `wt-retry-tier`, `wt-dl-redrive-count`, `wt-claim-check` — were dropped at every broker: second-level retry and the redrive cap worked in-memory and were **silently dead behind RabbitMQ/Kafka/NATS/ASB/Redis**. `MessageHeaders.IsAdapterOwned` now strips only the 8 re-derived keys.
- **Redis Streams tested** — 5 container tests, 5 consecutive clean runs. Deterministic: the test provisions the consumer group at `Beginning` itself, killing the startup race the NATS/Kafka/RabbitMQ suites paper over with 1–2s sleeps.
- **MessagePack + CloudEvents serializers** — first implementations besides STJ since the seam shipped in B1. CloudEvents is structured-mode only; binary mode needs per-attribute `ce-*` headers and the seam returns bytes with no header access.
- **Claim-check** (`Transport/ClaimCheck.cs`) — offload over the existing `src/Storage/` `IBlobStorage`, rehydrate as an `IConsumeFilter`. Blob is **never** deleted on consume: publish is fan-out, retries re-read, and a DLQ redrive re-reads days later. Retention sweep only, and the retention window *is* the redrive window.
- **Both B6 adapters reviewed and fixed.** ASB `SettlesInContext` lied under sessions (the session receiver is disposed while a pump worker may still hold an unsettled message). Redis had process-global mutable dead-letter state, so two buses in one process wrote to each other's DLQ and acked the wrong group.
- Suite 46 → 51.

**B8 · 2026-07-19 — claim-check wired + content-type honoured + Redis defects**
- **`EventEnvelope.RawBody`** (`ReadOnlyMemory<byte>?`) + `RawBodyType` + `WireBodyType` + `ToWireBody(serializer)` — the general send-side transformation seam. Claim-check uses it; §3.2's compression and envelope encryption need the same one. `RawBodyType` is load-bearing: the receiving adapter eagerly deserializes into whatever `wt-event-type` names, so a substituted wire body must declare its own type or it decodes as silent garbage.
- claim-check **send half wired** — offloader called in `TransportEventBus` before the publish; the double-serialize is gone (the size check *is* the serialization). Guarded against offloading a message that already carries a reference (redrive / delayed-retry re-publish). All 5 adapters honour `RawBody`.
- **`ContentType` now selects the deserializer** (`Serialization/MessageSerializerRegistry.cs`). Every adapter previously read `wt-content-type` into the envelope and then deserialized with the single injected serializer regardless, while the XML docs on both claimed otherwise. Selection reads the *declared* header, not the envelope's `"application/json"` fallback — otherwise a legacy no-header producer routes to JSON on a MessagePack-default container.
- **Redis `XACK` defect fixed** — `PurgePhantomsAsync` conflated a trimmed-away entry with one another consumer legitimately claimed, and acked both. `XACK` is group-scoped, so that erased the winner's PEL row and the message was unrecoverable if the winner died. Now discriminates via `XRANGE` absence.
- **Redis consume loop** no longer dies on a transient fault — per-round try, transient set (`RedisTimeoutException`/`RedisConnectionException`/`LOADING`/`CLUSTERDOWN`/…), 1s→30s backoff, resets on a clean round.
- `StartAsync` now blocks on NATS + Redis, restoring the stop→drain→release order. **Kafka already blocked** — the premise carried in this file since B7 was wrong for Kafka.
- `Saga` header literals compose from `ReservedPrefix`; `Azure.Messaging.ServiceBus` gained a direct `PackageReference` (it compiled only transitively).
- 33 tests added (serializers, claim-check, `IsAdapterOwned` round trip). Suite 51 → 85.

**B9 · 2026-07-19 — startup race + serializer contract + header cover**
- **`IBusControl` startup race fixed — a real SDK bug, not a test flake.** .NET 10 changed `BackgroundService`: `StartAsync` no longer runs `ExecuteAsync` inline, it schedules it on the thread pool and returns. `MarkRunning()` lived in `ExecuteAsync`, which the host never awaits — so `IHost.StartAsync()` could return with the bus still reporting `Stopped`. A caller that paused or queried straight after startup saw a bus that had never run. Moved to an awaited `StartAsync` override, **after** `base.StartAsync` so a transport that fails to start doesn't report a `Running` it never reached.
- the `StopHost` hypothesis carried in this file was **refuted** — `MarkStopped` never ran on the failing instances, and nothing faulted. B8's choice stands, untouched. Proven by trace instrumentation, then 5 clean full-suite runs + 3 more under 20 CPU hogs (pool starvation is the trigger).
- **STJ now returns null on an empty payload**, matching the interface contract and the two new serializers. Throwing was the fatal option, not the loud one: `TryReconstruct` is called *outside* the adapters' try/catch, so the `JsonException` escaped the consume loop and killed the whole subscription instead of costing one message.
- **`AddReceiveOnlyMessageSerializer<T>()`** — `AddMessageSerializer<T>()` uses `Replace`, so it could only *swap* the default and B8's content-type registry could never hold more than one entry. Fixed two ordering traps while wiring it: `Replace` removed the *first* match (dropping the extra format), and a bare additive call ahead of `AddEventResilienceDefaults` suppressed the STJ default entirely.
- `IsAdapterOwned` round trip extended from RabbitMQ to **Kafka, NATS and Redis** — one shared contract helper, 4 brokers, no per-broker drift.
- Suite 85 → 89, and the intermittent is gone: 4 consecutive clean full runs.

**B10 · 2026-07-19 — forgeable fields + saga routing + saga harness**
- **Redis `wt-body` forgery fixed.** Reserved but not adapter-owned after B7, so a caller-supplied `wt-body` rode the wire; stream entries are a **field list, not a map**, and `ReadBody` used the first-match indexer — the forgery replaced the real body. Fixed both ends: `RedisStreamsFields.IsAdapterOwned` strips Redis-only reserved fields on send, and `ReadBody` scans backwards for the last value. 4 more fields had the same exposure (`wt-dl-*` on the live stream); only `wt-body` was fatal because every other read goes through a last-wins dictionary.
- **Saga destinations now bind.** `EventSagaBuilder.SendsTo<TEvent>("dest")` → `AddDestinationBinding` → the alias rides `EndpointTopology.RoutingKeys`, so 4 key-routed brokers deliver it with **zero adapter edits**. An alias binds **only where its type is consumed locally** — binding blind would attach another service's destination to this queue and divert its traffic. `EventSagaOptions.UnroutableDestination` = `Warn` (default) / `Throw`.
- **Saga test harness** (`Testing.Messaging/SagaTestHarness.cs`) — transitions don't cross the bus, so it decorates `ISagaRepository<TState>` plus a pass-through filter for the causing envelope. `SagaCoordinator` is `internal` with no `InternalsVisibleTo`, so the repository is the only public seam every transition crosses. Outcomes: `Transitioned` · `Conflicted` · `Ignored` · `Faulted`; `Attempt > 1` makes the optimistic-concurrency replay a positive assertion.
- **Latent hazard found:** every SDK backoff sleeps on `TimeProvider`, so a saga test that fakes the clock **and** forces a conflict or fault hangs rather than fails. Today's tests escape only because the conflict test uses the real clock. The harness zeroes both delays.
- 7 saga tests added; correlation-miss (`Ignore` vs `Fault`), finalization/retention and stale-timeout-after-unschedule were all untested. Suite 89 → 96.

---

## Open follow-ups

- Kafka pause parks the consume thread → `max.poll.interval.ms` eviction risk. Fix = native `Consumer.Pause(partitions)`.
- `Ignore` acks with no consumed metric — needs `ConsumeOutcome.Ignored` + a `Transport/` call site.
- `Messaging.spec.md:69` understates the retry contract now classification exists.
- RabbitMQ.Client 7.0.0 skews publish seq numbers on concurrent failure; fixed in 7.1.x.
- RabbitMQ priority rides the wire but is not ranked — needs `x-max-priority` at declare, which fails redeclare on a live queue. Blocked on topology.
- Webhooks deliberately NOT on the serializer seam — `JsonOptionsPresets` would change every signed byte and property name. Needs opt-in or v2 signature.
- `EventId` 6401–6403 collide between `KafkaTransport` and `WebhookPublisher`.

---

## Remaining work

**Tier-B: complete.** All 12 seams shipped.

**Tier-C** (§3): ~18 transports (none built beyond the original 3) · serializer breadth (MessagePack/Protobuf/Avro/CloudEvents) · EIP patterns (claim-check, aggregator, scatter-gather) · ops/management · schema registry.

---

## BATCH 11 — next (4 lanes, disjoint)

| Lane | Owns | Build |
|---|---|---|
| 1 | `RabbitMq/**` | Unroutable-publish detection, the half B10 lane 2 couldn't reach. Opt-in `RabbitMqOptions.FailOnUnroutable` → `mandatory: true` + `IChannel.BasicReturnAsync` on the send channel, **re-hooked in `GetChannelAsync` after every recreate**. Correlate via a pending map keyed by `MessageId`, registered **before** the publish — AMQP delivers the return ahead of that message's confirm ack, which B3 made `BasicPublishAsync` await. Default off: publishing before consumers declare their queues is normal. Weigh the cheaper `alternate-exchange` alternative first — no per-publish cost, and it catches drops from services that never opted in |
| 2 | `Messaging.Tests/**` | Pin B10's fixes. Redis: forged `wt-body` + 4 `wt-dl-*` stripped on send (`XRANGE` shows exactly one `wt-body`); a hand-rolled `XADD` with two `wt-body` fields resolves to the last — the only assertion that fails if the read-side half is reverted. Topology: a declared alias for a **consumed** type appears in `RoutingKeys`, one for a non-consumed type does **not** (the anti-steal rule). Also the missing `AddReceiveOnlyMessageSerializer` test (flagged B9) |
| 3 | `Kafka/**` + `Nats/**` (comments) + `Messaging/Messaging.md` + `Messaging.spec.md` | Stale post-B7 comments: `KafkaTransport.cs:117`, `NatsTransport.cs:137` say "minus the reserved namespace" but filter on `IsAdapterOwned`; `:118` claims an unfiltered caller header would "win the decode" — it would lose. Docs: `Messaging.md:52,65` and `Messaging.spec.md:212-215,285` don't mention `SendsTo` / `AddDestinationBinding` / the saga harness |
| 4 | `AwsSqs/**` (new) | Next adapter (§3.1) — SQS+SNS: native redrive DLQ, FIFO groups, visibility-timeout settlement. Follow the ASB/Redis structure; capability flags honest per B2's rule |

**Traps for BATCH 11:**
- **`TimeProvider` + backoff deadlock.** Every SDK backoff sleeps on `TimeProvider`, so any test that fakes the clock **and** forces a retry/conflict hangs instead of failing. `SagaTestHarness` zeroes both delays; a new harness must do the same.
- **Kafka's forgery test passes vacuously** — duplicate headers + last-write-wins means a "strip nothing" mutation still passes. Needs a raw consumer, blocked by the macOS librdkafka multi-consumer crash. Linux CI.
- ASB's `IsAdapterOwned` round trip stays unproven — no Testcontainers image.
- Redis: an instance created **and** finalized by one message under `RemoveOnFinalize` is never written, so it records as `Ignored`, not `Transitioned`.

---

## BATCH 10 — done (kept for reference)

| Lane | Owns | Build |
|---|---|---|
| 1 | `Transport/TransportEventBus.cs` + `MessagingContracts.cs` + `Transport/ClaimCheck.cs` | **Wire up claim-check's send half.** B7 shipped the consume half only — offload has no call site, so the feature is half-dead. Add `EventEnvelope.RawBody` (`ReadOnlyMemory<byte>?`, pre-serialized wire body), an optional `ClaimCheckOffloader?` on `TransportEventBus`, and one call before `sendTransport.SendAsync`. `RawBody` is also the seam §3.2's compression and envelope-encryption need |
| 2 | all 5 adapters (`RabbitMq` `Kafka` `Nats` `AzureServiceBus` `RedisStreams`) | Honour `EventEnvelope.RawBody` (`envelope.RawBody?.ToArray() ?? serializer.Serialize(...)`) and stamp `ClaimCheckOffload.Headers`. Then fix the **content-type-never-honoured** defect: every adapter reads `wt-content-type` into `EventEnvelope.ContentType` and then deserializes with the single injected serializer regardless — the XML docs on both claim it selects the deserializer. Needs a content-type→serializer registry |
| 3 | `RedisStreams/**` | Two defects the test lane found and did not fix: (a) `PurgePhantomsAsync` assumes `XACK` is consumer-scoped — **it is group-scoped**, verified against `redis:7-alpine`, so a losing claimer deletes the winner's PEL row and the message is unrecoverable if the winner dies; (b) `ConsumeLoopAsync` catches only `OperationCanceledException`, so a `RedisTimeoutException` kills consumption permanently with no log and no restart |
| 4 | `Messaging.Tests/**` | Tests for B7's untested work: MessagePack + CloudEvents round-trip (incl. a hand-written non-.NET CloudEvent with shuffled attributes and `data_base64`) · claim-check offload/rehydrate/missing-blob/forged-path · the `IsAdapterOwned` fix (a `wt-retry-tier` header must now survive a broker round-trip) |

**Traps for BATCH 8:**
- lane 1 and lane 2 are sequenced by `RawBody` — lane 2 can't compile its half until lane 1's field lands. Run lane 1 first, or have lane 2 write against the agreed field name.
- **`StartAsync` returns before the consume loop ends** on Kafka, NATS and Redis, so `BackgroundService.ExecuteAsync` completes at startup and the stop→drain→release order collapses. ASB and RabbitMQ block correctly. Cross-adapter fix, deferred twice now — worth its own lane.
- `Azure.Messaging.ServiceBus` has **no direct `PackageReference`**; it compiles transitively via `AspNetCore.HealthChecks.AzureServiceBus`. Add it to the messaging `ItemGroup`.
- `Saga/SagaContracts.cs` still spells `wt-saga-*` as raw literals instead of composing from `ReservedPrefix` — last site bypassing it.
