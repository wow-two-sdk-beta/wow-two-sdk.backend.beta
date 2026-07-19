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
dotnet test Messaging.Tests/WoW.Two.Sdk.Backend.Beta.Messaging.Tests.csproj -m:1     # Docker. 29 pass / 1 skip
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

**Tier-B, 4 seams left** (§2): `ITopologyProvider` + `IEndpointNameFormatter` · `IRequestClient` · state-machine saga + `ISagaRepository` · typed header model + propagation policy.

**Wave 2 leftovers** (§1): A8 re-enqueue delayed retry (in-proc `Task.Delay` still holds the message unsettled) · Kafka true delivery-count.

**Tier-C** (§3): ~18 transports · serializer breadth · DLQ redrive + 2nd-level retry · EIP patterns · test harness · ops/management.

---

## BATCH 4 — next (4 lanes, disjoint)

| Lane | Owns | Build |
|---|---|---|
| 1 | `Transport/Topology*.cs` (new) + `RabbitMq/**` | `ITopologyProvider` + `IEndpointNameFormatter`; replace the `#` catch-all with per-type routing; per-endpoint DLQ; add `x-max-priority` at declare |
| 2 | `MessagingContracts.cs` + `Transport/MessageHeaders*.cs` (new) | typed header model + propagation policy; only `traceparent` flows today; `ReplyTo` on the envelope (prereq for `IRequestClient`) |
| 3 | `Reliability/EventResilience.cs` + `Reliability/Retry*.cs` (new) | A8 re-enqueue delayed retry via `IEventScheduler`, so backoff stops pinning a consumer slot |
| 4 | `src/Testing/**` + `Messaging.Tests/TestSupport.cs` | `MessagingTestHarness` on the observer seam — published/consumed/faulted assertions, idle-wait; every test re-rolls `EventCollector` today |

Lane 2 before lane 1 finishes is fine — different files. `IRequestClient` is BATCH 5, after lanes 1+2 land `ReplyTo` + temp-queue topology.

**Sequencing note:** lane 1 and lane 3 both change observable delivery behaviour — run the full suite after each, not just at the end of the batch.
