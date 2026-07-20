# Messaging — spec

*Last updated: 2026-07-19*

> **Concrete API + usage.** Updated when the public surface changes.

## NuGet

```
WoW.Two.Sdk.Backend.Beta  (mono-lib; namespace WoW.Two.Sdk.Backend.Beta.Messaging[.*])
```

## Public API

### Contracts (`WoW.Two.Sdk.Backend.Beta.Messaging`)

| Type | Notes |
|---|---|
| `IEvent` | Marker for a bus event. **The bus accepts `IEvent` only** — commands/queries stay on the mediator. Topology (in-mem vs broker) and scope (local vs cross-service) are absent from the contract by design. |
| `IEventHandler<TEvent>` | `ValueTask HandleAsync(EventContext<TEvent>, CancellationToken)`; `where TEvent : class, IEvent`. Many per event type. |
| `IEventBus` | `PublishAsync<T>(T, PublishOptions?, ct)` (fan-out) · `SendAsync<T>(string destination, T, SendOptions?, ct)` (point-to-point). Both `where T : class, IEvent`. |
| `EventContext<TEvent>` | `Event` · `Envelope` · `MessageId` · `CorrelationId` · `ConversationId` · `ReplyTo` · `Headers` · `PublishAsync`/`SendAsync` (auto-propagate correlation, set `CausationId`, and apply the header-propagation policy). |
| `EventEnvelope` | `record`: `MessageId`, `Body`, `BodyType`, `Destination`, `CorrelationId`, `ConversationId`, `CausationId`, `ReplyTo`, `DeliveryCount`, `NotBeforeUtc`, `PartitionKey`, `Durable`, `Priority`, `TimeToLive`, `Headers`. Wire-substitution seam: `RawBody` · `RawBodyType` · `WireBodyType` · `ToWireBody(serializer)` — see below. |
| `EventEnvelope.RawBody` (+ `RawBodyType`) | Pre-serialized wire bytes, for a send-path transformation that must decide what travels **before** the adapter serializes. `null` (default) leaves every adapter serializing `Body` itself. `RawBodyType` names the shape those bytes decode as when the transformation substituted a *different* one; it governs the `wt-event-type` token only (`WireBodyType = RawBodyType ?? BodyType`) and **never routing**, which stays on `BodyType` so the message still reaches consumers bound to the real contract. A general seam, not one feature's field — claim check (pointer), compression (same body, deflated) and envelope encryption (ciphertext) are the same shape. It also removes a double serialization for any transformation that has to *measure* the body. |
| `EventEnvelope.ToWireBody(IMessageSerializer)` | The bytes to put on the wire: `RawBody` where something pre-serialized them, else `Body` through the serializer. **Every adapter MUST call this** rather than serializing directly — it is the single expression deciding whether a transformation is honoured. |
| `PublishOptions` / `SendOptions` | `MessageId`, `CorrelationId`, `ConversationId`, `CausationId`, `ReplyTo`, `Delay`, `PartitionKey`, `Durable`, `Priority`, `TimeToLive`, `Headers`. **Transport-abstract** — adapters map to native primitives or ignore. |

### Serialization (`…Messaging.Serialization`)

| Type | Notes |
|---|---|
| `IMessageSerializer` | Body ↔ bytes, with a `ContentType`. Default `SystemTextJsonMessageSerializer` (`JsonOptionsPresets`). Swap via `AddMessageSerializer<T>()`. |
| `IMessageTypeResolver` | CLR type ↔ **stable wire token** (`FullName`, AQN fallback). The token — not the assembly-qualified name — is what rides `wt-event-type` and what a routing key is built from, so a version bump or type move does not break routing. |
| `MessageTypeRegistry` · `MapMessageType<TEvent>(token)` | Explicit token for a producer-only contract, a custom URN, or a rename alias. |
| `MessagePackMessageSerializer` | Compact binary — typically 30–60% smaller than JSON and cheaper to encode, at the cost of a payload unreadable off the broker. `ContentType` = `application/x-msgpack`. Contractless resolver by default, so a contract that worked under JSON keeps working with no `[MessagePackObject]` annotation. Security: `MessagePackSecurity.UntrustedData`; the **typeless** resolvers are deliberately not used — they embed CLR type names and instantiate whatever the sender names (RCE vector). Type identity stays in `wt-event-type`, never in the body. Compression is off by default and changes the wire bytes, so it must be switched on producers and consumers together. |
| `CloudEventsMessageSerializer` · `CloudEventsSerializerOptions` | CNCF CloudEvents 1.0 **JSON event format** — the interop format for non-.NET peers on the same streams. `ContentType` = `application/cloudevents+json`. **Structured mode only**: `IMessageSerializer` returns bytes and cannot write transport headers, so binary mode is not expressible at this seam (structured is also the self-describing one — it survives a broker that drops unknown headers). Attributes: `specversion` = `1.0` · `type` = `IMessageTypeResolver.ToTypeToken` (so `MapMessageType<T>("com.acme.orders.placed")` is how a contract gets a reverse-DNS type) · `source`/`subject`/`dataschema` from options · `time` RFC 3339 UTC. **Does not map**: `id` is generated here, not taken from `MessageId`, and inbound `id`/`source`/`time`/`subject` are parsed then dropped — the seam passes only the body. A consumer deduplicating on CloudEvents `source`+`id` and the SDK inbox deduplicating on `wt-message-id` are therefore keyed on different values; point `IdFactory` at a business key when that matters. Options take no argument on `AddMessageSerializer<T>()` — register `CloudEventsSerializerOptions` as a singleton first. |

### Wire headers & propagation (`…Messaging.Transport`)

| Type | Notes |
|---|---|
| `MessageHeaders` | The shared wire-header contract, so no adapter carries inline literals. `ReservedPrefix = "wt-"` · `EventType` · `ContentType` · `MessageId` · `PartitionKey` · `ReplyTo` · `CorrelationId` · `ConversationId` · `DeliveryCount` · `DeadLetterReason` · `DeadLetterExceptionType`; unreserved standards `TraceParent` · `TraceState` · `Baggage`. Two predicates, **not interchangeable**: `IsReserved(key)` · `IsAdapterOwned(key)`. |
| `IsReserved` ⊃ `IsAdapterOwned` | `IsReserved` = the whole `wt-` namespace. `IsAdapterOwned` = the **8 keys an adapter re-derives from the envelope on every send** (`EventType`, `ContentType`, `MessageId`, `PartitionKey`, `ReplyTo`, `CorrelationId`, `ConversationId`, `DeliveryCount`) — the only ones it may strip. Everything in the gap is a reserved header an SDK **feature** stamps and no adapter re-stamps: `wt-slr-tier`, `wt-dl-redrive-count`, `wt-claim-check*`, `wt-saga-timeout-*`. Stripping on `IsReserved` drops all of them at the broker, so those features work in-memory and are silently dead behind every broker — the bug this pair exists to prevent. |
| *reserved namespace* | An adapter re-stamps the adapter-owned 8 from the envelope, so a caller-supplied copy of one is **overwritten on send** and cannot forge the wire contract (a forged `wt-event-type` would otherwise redirect type resolution). Reserved-but-not-owned headers **ride through** a send, which is what lets a retry, delay or redrive hop carry a claim-check reference or a retry tier. Separately, `MessageHeaderPropagation` blocks **every** reserved key from flowing consumed → published: a control header describes the message it arrived on, so carrying one forward would stamp the previous body's type token or id onto a new body. |
| `IMessageHeaderPropagationPolicy` | `ShouldPropagate(key)` — which of a consumed message's headers flow onto a message published *while handling it*. Applied by `EventContext<T>`, the only place holding both. |
| `MessageHeaderPropagationPolicy` | Allow-list impl, case-insensitive, immutable (`Allow(…)` returns a widened copy). `Default` = `traceparent` + `tracestate` · `None` = nothing. Reserved keys can never be allowed. |
| `MessageHeaderPropagation.BuildOutboundHeaders(policy, inbound, explicit?)` | Merge: policy-allowed inbound headers, overlaid with headers the caller set explicitly (caller wins). Returns `null` when empty, so an unconfigured path stays allocation-free. |

### Topology (`…Messaging.Transport`)

Replaces the pre-B4 catch-all binding: bindings derive from the **registered handler set**, one routing key per consumed message type, so a service receives only the types it handles instead of filtering the whole exchange in-process.

| Type | Notes |
|---|---|
| `ITopologyProvider` | `ConsumeEndpoints` · `RoutingKeyFor(Type)` · `ResolveRoutingKey(EventEnvelope)` (type key for a publish, destination address for an explicit send). |
| `EndpointTopology` | `record`: `Queue` · `DeadLetterQueue` · `RoutingKeys` · `MessageTypes`. |
| `IEndpointNameFormatter` | `Endpoint(Type)` · `DeadLetter(endpointName)`. Default kebab-cases the **simple** type name under an optional dotted prefix: `OrderPlaced` + `wt.events` → `wt.events.order-placed` → `.dlq`. |
| `TopologyStyle` | `SharedEndpoint` (**default** — one queue per service, one binding per type; existing deployments keep their queue + DLQ names and nothing strands) · `EndpointPerMessageType` (per-type queue, prefetch, DLQ — opt-in, needs new queues). |
| `TopologyOptions` | `Style` · `SharedEndpointName` · `SharedDeadLetterQueueName` · `EndpointPrefix` · `BindLegacyTypeNameKeys` (default `true` — keeps un-upgraded publishers' short-name keys routable) · `BindEndpointNameKeys` (default `true` — an explicit `SendAsync` to a queue name lands point-to-point). |
| `ConsumedMessageTypeRegistry` | Registration-time consumed-type set, ordered by full name so the declared topology is identical across restarts and instances. |

Routing keys are sanitized: `#` / `*` can never reach a key, and AMQP's 255-char short-string limit is enforced.

### Concurrency — the pump (`…Messaging.Transport`)

A receive loop hands each message to the pump instead of awaiting the pipeline inline, so the loop keeps pulling while handlers run on worker tasks. Every transport routes through it.

| Type | Notes |
|---|---|
| `ConcurrencyOptions.MaxConcurrentMessages` | Default `1` — **behaviour-preserving**: dispatch stays inline and sequential, exactly as before the pump existed. `> 1` starts that many workers. Forced to 1 for a transport reporting `ThreadAffineConsume`. |
| `ConcurrencyOptions.MaxQueuedMessagesPerWorker` | Default `1` — the pump backpressures the consume loop as soon as every worker is busy, leaving unclaimed messages at the broker. |
| `ConcurrencyOptions.PreserveKeyOrder` | Default `true` — FNV-1a over `PartitionKey` routes same-key messages to the same worker; unrelated keys run in parallel. Keyless messages round-robin either way. |
| `ConcurrencyOptions.DrainTimeout` | Default `30s` — shutdown budget for in-flight handlers. |

A broker's own prefetch/fetch window should be at least `MaxConcurrentMessages` or it becomes the limit instead.

### Bus control (`…Messaging.Transport`)

| Member | Notes |
|---|---|
| `IBusControl.State` | `BusState`: `Stopped` · `Running` · `Paused` · `Draining`. Stamped by the transport hosted service as well as by explicit calls, so it reflects the host. |
| `IBusControl.InFlight` | Messages queued to a worker or executing. Messages parked at the pause gate are **not** counted — they never entered the pipeline. |
| `IBusControl.PauseAsync(ct)` | **Backpressure, not buffering** — shuts an async gate at the head of dispatch. Nothing is dropped or queued in-process; unconsumed messages stay unacknowledged at the broker, where prefetch throttles delivery. Gates *entry* only: work already in flight runs to completion. |
| `IBusControl.ResumeAsync(ct)` | Releases every consume loop parked at the gate. |
| `IBusControl.StopAsync(ct)` | Shut the gate → wait for in-flight → retire workers, the whole sequence bounded by `DrainTimeout`. Does **not** throw on timeout: an overrunning handler is logged and the bus reports `Stopped` either way. Terminal — the bus does not restart in place. |

Every method is idempotent, so a retried ops call cannot fail; a nonsensical transition (resuming a stopped bus) throws `InvalidOperationException` rather than pretending to work. Registered as a singleton by every transport path — resolvable from an ops endpoint, an admin command, or a health check.

### Observers (`…Messaging.Transport`)

Watch-only counterpart to `IConsumeFilter`. A filter sits *in* the chain and may short-circuit, transform, or swallow; an observer is notified out of band, is handed no continuation, and **cannot change settlement** — an exception it throws is caught and logged. Observers are singletons and must be thread-safe. Zero-observer cost is a `.Length` check.

| Interface | Hooks | Cardinality |
|---|---|---|
| `IPublishObserver` | `PrePublishAsync` · `PostPublishAsync` · `PublishFaultAsync` | once per send |
| `IReceiveObserver` | `PreReceiveAsync` · `PostReceiveAsync` · `ReceiveFaultAsync` | once per **message**, spanning settlement (fault hook fires *after* dead-lettering, so the message is already at rest) |
| `IConsumeObserver` | `PreConsumeAsync` · `PostConsumeAsync(ConsumeOutcome)` · `ConsumeFaultAsync` | once per **delivery attempt**, inside the resilience loop |

`AddMessageObserver<T>()` surfaces one instance under every observer interface it implements; a type implementing none throws at registration rather than silently doing nothing.

### Reliability (`…Messaging.Reliability`)

| Type | Notes |
|---|---|
| `BackoffKind` · `RetryConfig(MaxAttempts=5, Backoff=ExponentialJitter, BaseDelay?, MaxDelay?)` · `IRetryPolicy` · `DefaultRetryPolicy` | Retry schedule. `NextDelay` returns `null` → dead-letter. |
| `IDeadLetterStore` | `DeadLetterAsync(DeadLetterRecord, ct)` · `ReadAsync(source, ct) → IAsyncEnumerable<DeadLetterRecord>` · `ReplayAsync(messageId, ct)` (redrive). Build the record with `DeadLetterRecord.From(env, ex, nowUtc)`. |
| `IInboxProcessor.ProcessOnceAsync(messageId, handler)` | Exactly-once seam: dedupe + (EF) run handler in the **same transaction**. `false` ⇒ duplicate (skip). |
| `IEventScheduler.ScheduleAsync(envelope, notBeforeUtc)` | Delayed delivery. |
| `IOutbox` / `OutboxRecord` / `IOutboxDispatcher` | Transactional-outbox ports. Rows carry `content_type`, so the outbox rides the serializer seam. |

### Fault classification (`…Messaging.Reliability`)

Ends every fault burning the whole `MaxAttempts` budget. Consulted by **every** `IEventResiliencePipeline` implementation, so behaviour does not depend on which one is registered.

| Type | Notes |
|---|---|
| `FaultDisposition` | `Retry` (**default for every exception** — an unconfigured pipeline behaves exactly as before classification existed) · `DeadLetter` (propagate at once, spending no attempt) · `Ignore` (swallow, so the consume pipeline's success path acknowledges). |
| `IEventFaultClassifier.Classify(Exception)` | Thread-safe, SHOULD NOT throw — runs on the consume path for every failed attempt. |
| `EventFaultClassificationOptions` | Ordered rules, **first match wins**: `DeadLetterOn<T>()` · `IgnoreOn<T>()` · `RetryOn<T>()` (match subclasses too) and `Classify(Func<Exception, FaultDisposition?>)` for state-dependent verdicts. A rule that throws is treated as no verdict. |
| `DefaultEventFaultClassifier` | Walks the rules, first non-null verdict wins, falls back to `Retry`. `RetryAll` is the registered default. |

Cancellation is never classified: an `OperationCanceledException` from a cancelled token always propagates as shutdown, never as a message fault.

### Delayed retry (`…Messaging.Reliability`)

Moves the backoff out of the consume slot: the failed delivery is re-published with a future `NotBeforeUtc` and **then** acknowledged (a crash in between = redelivery, not loss), so the consumer slot is free for the whole wait.

| Type | Notes |
|---|---|
| `DelayedRetryOptions.Enabled` | Default `false` — opt in with `AddDelayedEventRetry(…)`. The two modes differ in observable semantics: a retry arrives as a **fresh delivery** (the whole filter chain re-runs, not just the core), the message is acknowledged between attempts (broker redelivery timers, prefetch accounting and in-flight metrics see it leave and come back), and the attempt number rides `EventEnvelope.DeliveryCount` instead of a local. |
| `DelayedRetryOptions.Retry` | `null` (default) shares the in-process pipeline's schedule, so relocating the wait does not silently change the policy. Set it to give the re-enqueue loop its own budget. |

Requires a transport that can hold a message until a future instant (`NativeDelay` / `NativeScheduling`) plus a registered `IEventScheduler`; where either is missing the in-process delay is used and the downgrade is logged **once at startup**, not per message. Three independent stops prevent an infinite redelivery loop. A `DeadLetter` verdict or an exhausted budget falls through to the normal dead-letter path.

### EF reliability (`…Messaging.Reliability.Ef`)

| Type | Notes |
|---|---|
| `OutboxMessageEntity` · `OutboxModelBuilderExtensions.ApplyOutboxModel()` | Maps `outbox_messages` (incl. `content_type varchar(100) NOT NULL DEFAULT 'application/json'`). |
| `EfOutbox<TContext>` · `AddEfOutbox<TContext>()` | `IOutbox` — direct-adds a row to `TContext` (atomic on `SaveChanges`). |
| `OutboxDispatcherOptions` · `AddEfOutboxDispatcher<TContext>()` | Polls pending → deserialize by `type` + `content_type` → publish to `IEventBus` → stamp `ProcessedOnUtc`. |
| `IOutboxClaimStrategy` · `PollingOutboxClaimStrategy` | Pending-row claim seam. Default polls (single-instance); a Postgres `FOR UPDATE SKIP LOCKED` impl plugs in for multi-instance scale-out. |
| `InboxMessageEntity` · `ApplyInboxModel()` · `AddEfInbox<TContext>()` | `IInboxProcessor` — inbox row + handler in **one transaction** (true exactly-once). |

Migration SQL + wire-up: [`Reliability/Ef/Ef.md`](./Reliability/Ef/Ef.md).

### In-memory transport (`…Messaging.InMemory`)

| Type | Notes |
|---|---|
| `InMemoryEventBusOptions` | `ChannelCapacity` (default 1024; 0 = unbounded) · `Retry` (`RetryConfig`). |
| *(internal)* `InMemoryEventBus`, `EventConsumerHostedService`, `InMemory{DeadLetterStore,InboxStore,EventScheduler}` | Registered by `AddInMemoryEventBus`. |

### Azure Service Bus transport (`…Messaging.AzureServiceBus`)

Topic + one subscription per topology endpoint, each carrying one `CorrelationRuleFilter` per routing key. The routing key rides the native `Subject`, which is what the filter matches — the Service Bus analogue of a RabbitMQ topic binding, and the cheapest filter kind because the broker evaluates it against an indexed property.

| Type | Notes |
|---|---|
| `AddAzureServiceBusEventBus(Action<AzureServiceBusOptions>, params Assembly[])` | Handlers + resilience + topology + send/receive transports + `IEventBus` + consumer hosted service. Back-fills `TopologyOptions.SharedEndpointName` from `Subscription`; **no** dead-letter name — the DLQ is the entity's own `$DeadLetterQueue` sub-queue, not a second subscription. |
| `AzureServiceBusOptions` | `ConnectionString` (**required**) · `Topic` (`wt-events`) · `Subscription` (`wt-events`) · `ProvisionEntities` (`true`) · `MaxDeliveryCount` (10) · `LockDuration` (60s, the ASB max) · `PrefetchCount` (0) · `MaxMessagesPerReceive` (10) · `ReceiveWaitTime` (30s) · `RequiresSession` · `MaxConcurrentSessions` (1) · `SessionIdleTimeout` (30s) · `EnableDuplicateDetection` · `DuplicateDetectionWindow` (10m) · `RemoveCatchAllRule` · `UseWebSockets`. |
| *(internal)* `AzureServiceBus{Connection,Topology,SendTransport,ReceiveContext,ReceiveTransport,Capabilities}` | One shared `ServiceBusClient`; provisioning swallows "already exists" so concurrent instances race safely. |

**Envelope → native property**, not headers: `MessageId` · `CorrelationId` · `ReplyTo` · `ContentType` · `TimeToLive` · `NotBeforeUtc` → `ScheduledEnqueueTime` · `PartitionKey` → `SessionId` (session entity) or `PartitionKey` (non-session), never both. Only `wt-event-type`, `wt-content-type`, `wt-partition-key` and `wt-conversation-id` ride application properties — AMQP has no property for them.

Built on `ServiceBusReceiver`, **not** `ServiceBusProcessor`: the processor stops renewing a message's lock once its handler returns, which would race the pump's off-thread settlement. Entity provisioning seeds `$Default` with a **false** filter, so a new subscription never briefly matches every message on the topic before its real rules land. Names are sanitized to the ASB charset and truncated with a stable FNV-1a suffix (50 chars for a subscription/rule, 260 for a topic), so two long type tokens cannot collapse onto one rule.

### Redis Streams transport (`…Messaging.RedisStreams`)

`XADD` per message, `XREADGROUP` + consumer group per service, and an `XPENDING`/`XCLAIM` sweep that recovers entries stranded in a dead instance's pending-entries list (PEL) — the failure this adapter exists to survive.

| Type | Notes |
|---|---|
| `AddRedisStreamsEventBus(Action<RedisStreamsOptions>, params Assembly[])` | Handlers + resilience + topology + send/receive transports + `IEventBus` + consumer hosted service. Back-fills `TopologyOptions.SharedEndpointName` from `Stream` and `SharedDeadLetterQueueName` from `DeadLetterStream`. |
| `RedisStreamsOptions` | `Configuration` (`localhost:6379`) · `Database` (-1) · `Stream` (`wt.events`) · `ConsumerGroup` (`wt-consumers`) · `ConsumerName` (null → `{machine}-{pid}`) · `DeadLetterStream` (`wt.events.dlq`) · `MaxLength` (100 000) · `UseApproximateMaxLength` (`true`) · `DeadLetterMaxLength` (null = never trimmed) · `BatchSize` (32) · `PollInterval` (250ms) · `ClaimInterval` (30s) · `MinIdleTimeBeforeClaim` (5m) · `MaxDeliveryAttempts` (5) · `RouteByDestination`. |
| *(internal)* `RedisStreams{Connection,Topology,WireFormat,SendTransport,ReceiveContext,ReceiveTransport,Capabilities}`, `RedisStreamName` | One shared `ConnectionMultiplexer`; `RedisStreamsFields` aliases `MessageHeaders` for every cross-broker key. |

A stream entry is a flat field/value map, so every header is its own field alongside `wt-body`. The loop **polls** rather than blocking: StackExchange.Redis multiplexes every command over one connection and exposes no `BLOCK` argument, so a blocking read would stall every other command — `PollInterval` is therefore the idle latency floor and applies only after an empty read.

- **`RouteByDestination`** (opt-in) nests a stream per routing key under `Stream` (`wt.events.order-placed`), and a consumer reads `Stream` **as well as** every routed stream — which is what makes a consumers-first rollout possible. On Redis Cluster give `Stream` a hash tag (`{wt.events}`) so the root and its children share a slot, or a multi-stream `XREADGROUP` answers CROSSSLOT.
- **`MinIdleTimeBeforeClaim`** is the one value that decides whether recovery duplicates live work: idle is measured from last delivery, not last sign of life, so a handler still running after it looks exactly like a dead one. Keep it above the slowest handler's worst case.
- **Trimming vs the PEL** — `MAXLEN` can evict an entry still pending, leaving a PEL row pointing at data that no longer exists. Detection is an **`XRANGE` absence check**, one per id the claim did not recover: the entry is acked only when the stream no longer holds it. `XCLAIM` returning nothing is *not* on its own evidence of a phantom — another instance claiming the id first resets its idle clock and declines this claim identically — and `XACK` is scoped to the **group**, not the consumer, so acking on that signal alone would erase the live pending row of whichever consumer just won it, making the message unrecoverable if that consumer then died. The absence check is safe whoever owns the row (with the data gone nobody can process it) and cannot go stale: ids are monotonic and `XADD` rejects any id at or below the tail, so a trimmed id can never come back. Cost of a genuine phantom is a lost message rather than a permanently stuck one.
- **DLQ is emulated** — `XADD` the original to `DeadLetterStream`, *then* `XACK`. That order can duplicate into the DLQ on a later claim; the reverse loses the message outright.

### Broker capability matrix

What each adapter's `ITransportCapabilities` reports, and why. Conditional support reports **false** — a flag claiming a capability the adapter does not deliver is worse than none, because the pipeline branches on it to choose native-versus-emulated.

| Flag | ASB | Redis Streams | Note |
|---|---|---|---|
| `NativeDeadLetter` | ✓ | ✗ | ASB `$DeadLetterQueue` sub-queue with reason + description. Redis has no facility — SDK re-adds to a second stream. |
| `NativeDelay` | ✓ | ✗ | `ScheduledEnqueueTime`. An `XADD`ed entry is immediately readable. |
| `NativeDedupe` | `EnableDuplicateDetection` | ✗ | Fixed at topic creation, so it tracks the option. `XADD` id monotonicity ≠ idempotency. |
| `NativeOrdering` | `RequiresSession` | ✓ | FIFO is a session guarantee on ASB. A Redis stream is a strictly ordered log — the *read* stays ordered, not the concurrent processing of what was read. |
| `NativePriority` | ✗ | ✗ | Neither ranks a queue by a per-message priority; both emulate with an entity per band. |
| `NativeTimeToLive` | ✓ | ✗ | ASB per-message `TimeToLive`, clamped by the entity default, dead-lettered on expiry. `MAXLEN`/`MINID` are stream-wide retention, which this flag excludes. |
| `NativeScheduling` | ✓ | ✗ | Absolute-instant enqueue, durable across a restart. Redis entry ids timestamp the write, not the reveal. |
| `NativeSessions` | `RequiresSession` | ✗ | Fixed when the entity is created. Redis consumers own individual entries via the PEL, not a key range. |
| `NativePublisherConfirms` | ✓ | ✓ | AMQP settled transfer. `XADD` answers with the assigned id — acceptance, though not fsync (`appendfsync everysec` + async replication). |
| `NativeTransactions` | ✗ | ✗ | ASB has `TransactionScope` batching but no idempotent producer; `MULTI`/`EXEC` is atomic execution only. Both are the shape this flag excludes. |
| `NativeRequestReply` | ✓ | ✗ | `ReplyTo`/`ReplyToSessionId` + `CorrelationId` are first-class ASB properties. Redis request-reply means provisioning a reply stream and correlating by field — the SDK's work. |
| `SettlesInContext` | `!RequiresSession` | ✓ | **Conditional on ASB**: a session receiver's message locks live inside the session lock, which the accept loop releases when the session drains — possibly while a pump worker still holds an unsettled message. Non-session receivers live until `StopAsync`. `XACK` is an ordinary command on a thread-safe multiplexer. |
| `ThreadAffineConsume` | ✗ | ✗ | Both consume over an ordinary async API, so the pump may dispatch in parallel. |

### Event saga (`…Messaging.EventSaga`)

| Type | Notes |
|---|---|
| `IEventSagaStep` / `EventSagaStep` | `Name` · `ExecuteAsync(EventSagaContext, ct) → EventSagaStepOutcome` · `CompensateAsync`. |
| `EventSagaStepOutcome` | `Completed()` / `Faulted(reason)`. |
| `EventSagaContext` | `CorrelationId` · `Seed` · `Items` · `Set` · `Get<T>`. |
| `EventSagaBuilder` | `Named(name)` → `.Step<T>()` / `.Step(Type)` → `.Build()`. |
| `EventSagaDefinition` | `Name` · `StepTypes` · `ToMermaid()`. |
| `IEventSagaRunner.RunAsync(definition, context, ct)` → `EventSagaResult` | Compensates completed steps in reverse on failure. |
| `IEventSagaTransport.SendAsync<TEvent>(destination, event, context, ct)` | Step-emitted event seam (`where TEvent : class, IEvent`). |

### Resilience (`…Messaging.Reliability` · `.Polly`)

| Type | Notes |
|---|---|
| `IEventResiliencePipeline.ExecuteAsync(action, ct)` | Owns the retry loop. Every implementation MUST route a thrown exception through the registered `IEventFaultClassifier` **before spending an attempt** and honour the verdict — `Retry` loops on the configured schedule and throws once exhausted (the consumer then dead-letters), `DeadLetter` rethrows immediately, `Ignore` returns normally so the caller acknowledges. MUST also stop after a **single attempt** while `DelayedRetryOptions.Enabled`, so the wait happens between deliveries rather than in this loop with the message unsettled. Default = `IRetryPolicy`-backed. |
| `PollyEventResilienceOptions` · `AddPollyEventResilience(…)` | Swap in a Polly pipeline (exp+jitter retry + optional circuit breaker + per-attempt timeout). Classification applies here identically. |

### Consume pipeline (`…Messaging.Transport`)

One received message: CONSUMER span (trace context extracted) → ordered `IConsumeFilter` chain → core (resilience → `IInboxProcessor` dedupe → dispatch) → settle. With no filters registered the chain is the bare core.

| Type | Notes |
|---|---|
| `IConsumeFilter.InvokeAsync(context, next, ct)` | Ordered by registration, first-registered = outermost, runs **once per message** around the core (fault-publish, wire-tap, claim-check, rate-limit, …). Unlike an observer it may short-circuit. |
| `ReceiveContext` | `Envelope` · `AcknowledgeAsync` · `DeadLetterAsync(reason, exception, ct)`. |

### Claim check (`…Messaging.Transport`)

Bodies over a size threshold go to blob storage and a small pointer travels instead, rehydrated before the handler sees it. Every broker caps message size (ASB Standard and SQS at 256 KB, Kafka at 1 MB by default) and an oversized body is rejected at publish with nothing the SDK can do about it.

**Off by default** — `AddEventClaimCheck(…)` is the opt-in and nothing is consulted until it is called, so an application that configures nothing puts the same bytes on the wire as before. Requires an `IBlobStorage` registration (`AddLocalBlobStorage` or a cloud adapter).

| Type | Notes |
|---|---|
| `AddEventClaimCheck(Action<ClaimCheckOptions>?)` | Offloader + rehydrate filter + retention sweeper + the `ClaimCheckReference` type-token registration. Sets `Enabled` **before** applying the caller's configuration, so a bound config section that omits the key cannot switch the wire format over by accident. **Call it last** — see ordering below. |
| `ClaimCheckOptions` | `Enabled` · `ThresholdBytes` (128 KiB — under the tightest common broker cap with room for headers) · `PathPrefix` (`messaging/claim-check`) · `Retention` (7d) · `SweepEnabled` (`true`) · `SweepInterval` (1h) · `MaxPayloadBytes` (64 MiB). |
| `ClaimCheckHeaders` | `Reference` (`wt-claim-check` — its **presence** is what marks a message as offloaded) · `Size` (`wt-claim-check-size`) · `BodyType` (`wt-claim-check-type`) · `TryReadReference(envelope, out path)`. |
| `ClaimCheckReference` | `Path` · `SizeBytes` · `ContentType` · `BodyType`. Registered under the stable token `wt.claim-check-reference`, **not** the AQN fallback whose version segment moves on every SDK push and would strand a message across a version skew. Deliberately not an `IEvent` — a wire artefact, never publishable. |
| `ClaimCheckPayloadException` | The body could not be read back: blob gone, over `MaxPayloadBytes`, truncated, undeserializable, or a reference refused by the guards. Terminal — a redelivery reads the same reference and fails identically. |

- **Routing is preserved.** The wire bytes and `wt-event-type` become the reference's (`RawBody` + `RawBodyType`), while `Body`/`BodyType` stay the real contract — so `ResolveRoutingKey` still routes by it, metrics and observers still describe what was published, and the message lands where its consumers are bound.
- **One serialization per message.** Size is not knowable without serializing, so under the threshold the measured bytes are carried on `RawBody` and become the wire body rather than being produced twice.
- **Register it last.** Filter order is registration order and first-registered is outermost, so this must be innermost, wrapping only the dispatch core. A retry filter *inside* it would re-publish the **rehydrated** envelope — putting the whole body back through the send path and writing a second blob on every retry hop. Outside it, retry filters and `DeadLetterAsync` keep re-publishing the small reference envelope, which is what keeps the dead-letter store small and lets a redrive still work.
- **Blobs are never deleted on consume — only by retention.** Three things break otherwise: a fan-out's first finisher would delete the blob out from under the other subscribers; a retry re-reads the same blob; and a dead-letter record holds the pointer, so a redrive days later needs it. `Retention` **is** the redrive window — set it longer than the retry ladder plus dead-letter triage, or a redrive rehydrates nothing. On a store with its own lifecycle rules (S3, Azure blob) prefer those and set `SweepEnabled = false`.
- **A reference is attacker-reachable wire data.** It is normalized (rejecting `..` traversal segments) and then confined to `PathPrefix`; a reference pointing outside is refused **before** any store call, so the guard never becomes a read of an arbitrary blob on a shared store. `MaxPayloadBytes` caps the read so one forged pointer at a huge object cannot OOM the consumer.
- **A failed rehydrate spends no retry budget.** The filter throws *before* calling `next`, so the resilience pipeline — which lives inside the core it wraps — never runs and the message goes straight to the dead-letter path with the exception's message as its reason. Add `AddEventFaultClassification(r => r.DeadLetterOn<ClaimCheckPayloadException>())` if delayed or second-level retry is also configured and should not spend its budget either.
- **Roll out on consumers before producers.** A consumer without this registered has no rehydrator, and a claim-checked message reaches its handler with a body that was never fetched.

### Metrics (`…Messaging`)

Meter `WoW.Two.Sdk.Messaging` — auto-collected by `AddOpenTelemetryMetrics` via the `WoW.Two.*` prefix; a hand-rolled `MeterProvider` adds `MessagingMeter.Name` explicitly.

| Instrument | Name | Notes |
|---|---|---|
| Counter | `messaging.client.sent.messages` | Messages handed to the transport. |
| Counter | `messaging.client.consumed.messages` | Terminal outcome per received message, tagged `messaging.consume.outcome`. |
| Histogram (s) | `messaging.process.duration` | Time in the consume pipeline, retries and settlement included. |
| Counter | `messaging.process.dead_lettered.messages` | Tagged `error.type` — the exception **type** only; a message is unbounded. |
| Counter | `messaging.process.retried.messages` | In-process redelivery attempts (broker-side redelivery is invisible here). |
| Observable gauge | `messaging.process.in_flight.messages` | Several probes sum into one measurement. |

| Type | Notes |
|---|---|
| `ConsumeOutcome` | `Success` · `Faulted` (dead-lettered) · `Duplicate` (inbox skipped it) · `NoHandler` (settled without dispatch) · `Ignored` (an `IEventFaultClassifier` `Ignore` verdict swallowed the fault, so the message was acknowledged with no attempt completing). |
| `IMessagingMetrics` | `RecordPublished` · `RecordConsumed` · `RecordConsumeDuration` · `RecordDeadLettered` · `RecordRetried` · `TrackInFlight(Func<int>) → IDisposable`. Implementations MUST NOT throw (the call sites are unguarded) and MUST NOT tag with message id, correlation id or partition key — each is unbounded cardinality. |
| `NoOpMessagingMetrics.Instance` | Register before the transport to switch metrics off; the default registration is `TryAdd`-based and stands down. |

Tags: `messaging.destination.name` · `messaging.message.type` · `messaging.consume.outcome` · `error.type`.

### Registration (`…Messaging`) — composable

| Method | Notes |
|---|---|
| `AddInMemoryEventBus([Action?], params Assembly[])` | Batteries-included = handlers + reliability + transport. |
| `AddInMemoryEventTransport(Action?)` | Channel + `IEventBus` + consumer only. |
| `AddInMemoryReliability()` | In-mem `IRetryPolicy`/`IEventResiliencePipeline`/`IInboxProcessor`/`IDeadLetterStore`/`IEventScheduler`. |
| `AddEventResilienceDefaults()` | Transport-neutral resilience floor, reused by every broker adapter. |
| `AddEventHandlersFromAssemblies(params Assembly[])` | Scan + register handlers and `IEvent` contracts (incremental; callable repeatedly). |
| `AddEventSaga(EventSagaDefinition)` | Runner + in-proc transport + definition + step types. |
| `AddMessagingConcurrency(Action<ConcurrencyOptions>)` | Consume-side concurrency. Without it the pump runs at `MaxConcurrentMessages = 1`, dispatching inline. |
| `AddMessageTopology(Action<TopologyOptions>?)` | Endpoint/binding derivation. Also `AddEndpointNameFormatter<T>()` · `AddTopologyProvider<T>()`. |
| `AddEventFaultClassification(Action<EventFaultClassificationOptions>)` | Which exceptions skip the retry budget. Call order relative to the transport registration does not matter. Also `AddEventFaultClassifier<T>()`. |
| `AddDelayedEventRetry(Action<DelayedRetryOptions>?)` | Turn on re-enqueue-with-delay (sets `Enabled` before applying the caller's configuration). |
| `AddMessageObserver<TObserver>()` | One instance under every observer interface it implements. |
| `AddConsumeFilter<TFilter>()` | Ordered consume-pipeline filter. |
| `AddEventClaimCheck(Action<ClaimCheckOptions>?)` | Offload oversized bodies to `IBlobStorage`, send a pointer. Order vs the transport does not matter; order vs the other filter registrations does — **call it last**. |
| `AddMessageHeaderPropagation(IMessageHeaderPropagationPolicy)` | e.g. `MessageHeaderPropagationPolicy.Default.Allow("tenant-id")`. |
| `AddMessageSerializer<T>()` · `AddMessageTypeResolver<T>()` · `MapMessageType<TEvent>(token)` | Serialization seams. |
| `AddMessagingMetrics()` | `TryAdd`-based; every transport path already calls it. |

### Observability

`WoW.Two.Messaging` `ActivitySource` — PRODUCER span on publish/send (injects `traceparent` into `EventEnvelope.Headers`), CONSUMER span on process (extracts it, parenting the consumer span to the producer). Auto-collected by `AddOpenTelemetryTracing` via the `WoW.Two.*` wildcard source. Metrics: see above.

## Quick start — the user's chain (X → Y → back to X)

```csharp
builder.Services.AddInMemoryEventBus(typeof(Program).Assembly);

var saga = EventSagaBuilder.Named("checkout")
    .Step<ProcessOrderStep>()      // event X → X-handler, result → EventSagaContext
    .Step<ChargePaymentStep>()     //         → Y → Y-handler, result → EventSagaContext
    .Step<ConfirmOrderStep>()      //         → back to X (final)
    .Build();
builder.Services.AddEventSaga(saga);

var result = await runner.RunAsync(saga, new EventSagaContext(seed: cmd));
// a faulting step → reverse-order CompensateAsync on the completed steps.
```

## Usage examples

### Example A — consume in parallel, keep per-key order

```csharp
builder.Services.AddMessagingConcurrency(o =>
{
    o.MaxConcurrentMessages = 8;    // raise the broker's prefetch to at least this
    o.PreserveKeyOrder      = true; // same PartitionKey → same worker
    o.DrainTimeout          = TimeSpan.FromSeconds(45);
});
```

### Example B — stop burning the retry budget on faults that can't succeed

```csharp
builder.Services.AddEventFaultClassification(rules => rules
    .DeadLetterOn<ValidationException>()      // never succeeds on redelivery → dead-letter now
    .DeadLetterOn<JsonException>()
    .IgnoreOn<AlreadyAppliedException>());    // expected → acknowledge (counts as ConsumeOutcome.Ignored)
```

### Example C — free the consume slot during backoff

```csharp
builder.Services.AddDelayedEventRetry(o => o.Retry = new RetryConfig(MaxAttempts: 6));
// needs a transport with NativeDelay/NativeScheduling + an IEventScheduler;
// otherwise the in-process delay is kept and the downgrade is logged once at startup.
```

### Example D — pause the bus from an ops endpoint

```csharp
app.MapPost("/ops/bus/pause",  (IBusControl bus, CancellationToken ct) => bus.PauseAsync(ct));
app.MapPost("/ops/bus/resume", (IBusControl bus, CancellationToken ct) => bus.ResumeAsync(ct));
app.MapGet("/ops/bus",         (IBusControl bus) => new { bus.State, bus.InFlight });
```

### Example E — record what happened without changing it

```csharp
public sealed class AuditObserver : IReceiveObserver, IConsumeObserver
{
    public ValueTask PostConsumeAsync(ReceiveContext ctx, ConsumeOutcome outcome, CancellationToken ct) { /* … */ }
    // … remaining hooks
}

builder.Services.AddMessageObserver<AuditObserver>(); // one instance, both interfaces
```

### Example F — carry a header forward from consumed to published

```csharp
builder.Services.AddMessageHeaderPropagation(
    MessageHeaderPropagationPolicy.Default.Allow("tenant-id"));
// reserved "wt-" keys can never be allowed — a control header describes the message it arrived on
```

## Delivery semantics

- **At-least-once** floor → consumers MUST be idempotent; `IInboxProcessor` gives effectively-once by `MessageId`.
- **Ordering**: single bounded channel = FIFO write order. With `MaxConcurrentMessages > 1` order is preserved only *per `PartitionKey`* (`PreserveKeyOrder`, on by default); broker adapters map the key to native ordering and carry it as `wt-partition-key` where there is no native slot.
- **Settlement**: acknowledge on the pipeline's success path; dead-letter on exhaustion. A `DeadLetter` verdict settles on the first failure; an `Ignore` verdict acknowledges and counts as `ConsumeOutcome.Ignored`.
- **Pause** leaves messages unsettled at the broker rather than buffering them in-process, so prefetch throttles delivery and a restart loses nothing.

## See also

- [Messaging.standard.md](./Messaging.standard.md) · [Messaging.md](./Messaging.md)
- [`messaging-architecture-investigation.md`](../../docs/planning/messaging/messaging-architecture-investigation.md)
