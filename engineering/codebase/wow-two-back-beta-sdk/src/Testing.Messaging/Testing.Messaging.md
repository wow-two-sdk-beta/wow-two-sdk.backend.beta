# Testing.Messaging

Messaging test harness for the backend SDK — the companion to the dependency-light `Testing` package.

Holds the helpers that need the core mono-lib (the observer seam, `EventEnvelope`, `IEventBus`, `IBusControl`), so the base `Testing` package never takes that dependency. Reference `WoW2.Sdk.Backend.Beta.Testing.Messaging` from a test project only; it pulls core transitively.

## What it replaces

Every messaging test used to roll its own collector and poll with `Task.Delay` loops. The harness gives one assertion surface over the pipeline and one wait that is a condition instead of a guess.

```csharp
await using var harness = await MessagingTestHarness.StartAsync();

await harness.Bus.PublishAsync(new OrderPlaced("A-1"));

await harness.Consumed.WaitForAsync<OrderPlaced>();
harness.Published.Count<OrderPlaced>(e => e.Id == "A-1").Should().Be(1);
```

## Surface

- `MessagingTestHarness.StartAsync(...)` — builds + starts an in-memory bus with the recorder attached. `await using` stops it.
- `MessagingTestHarness.Attach(services)` — wraps a host the test built (`WebApplicationFactory`, broker-backed); register the observer with `services.AddMessagingRecorder()` first. Disposing does not stop that host.
- Logs: `Published` · `PublishFaults` · `Consumed` · `Faulted` · `DeadLettered`, each a `RecordedMessageLog` with `Count<T>` / `Any<T>` / `Of<T>` / `Bodies<T>` / `WaitForAsync<T>`, by event type or by predicate.
- `WaitForIdleAsync()` — returns once the bus has been silent for `QuietPeriod` **and** nothing is in flight. The primitive for asserting something did *not* happen.
- `WaitForInFlightZeroAsync()` — the narrower one: handlers have finished. Use after pause/stop, where no arrivals are possible.

## Phases are not interchangeable

- `Consumed` is per delivery **attempt** and carries the `ConsumeOutcome` — a deduplicated message shows up as `Duplicate`, not as a missing entry.
- `Faulted` is per attempt too, so its count **is** the retry budget spent. That is how a test tells a classified-fatal fault (1 entry) from an exhausted one (`MaxAttempts` entries) without timing it.
- `DeadLettered` is terminal and fires *after* settlement, so when a wait on it returns the dead-letter store is already written — read it directly, no polling.

## Broker hosts

`QuietPeriod` (default 100ms) is the harness's one assumption: a message handed to the transport reaches an observer within it. An in-process channel needs microseconds; raise it for a broker, where too short a window reports idle while a message is still on the wire.

## Sagas — `SagaTestHarness`

A state-machine saga needs its own harness, because a **transition never crosses the bus**: the coordinator loads state, runs the transition in-process and saves. The observer seam the message harness is built on cannot see any of that, so the saga harness records at the `ISagaRepository<TState>` instead — the one boundary every transition crosses, and the only public part of the saga runtime.

```csharp
await using var harness = await SagaTestHarness.StartAsync<OrderStateMachine, OrderSagaState>();

await harness.Bus.PublishAsync(new OrderPlaced("A-1", 100m));
await harness.WaitForStateAsync("A-1", OrderStateMachine.AwaitingPayment);

await harness.Bus.PublishAsync(new PaymentReceived("A-1", 5m));
var done = await harness.WaitForFinalizedAsync("A-1");
done.FromState.Should().Be(OrderStateMachine.AwaitingPayment);
```

### Surface

- `StartAsync<TStateMachine, TState>(...)` — in-memory bus + `AddSaga` + the recorder over the repository. `repository:` puts a durable or fault-injecting store underneath; a later `AddSagaRepository` would win over the recorder and silence it.
- State: `CurrentStateAsync(id)` · `GetInstanceAsync(id)` · `CountInstances()` · `PurgeFinalizedAsync(retention)`. All read the store *under* the recorder, so an assertion never shows up as a transition.
- Transitions: `Transitions` (`RecordedTransitionLog<TState>`) with `For(id)` / `Has<TEvent>(from, to)` / `Of<TEvent>(...)` / `WaitForAsync(predicate)`, and `HasTransition<TEvent>(from, to, id)` on the harness.
- Waits: `WaitForStateAsync(id, state)` · `WaitForFinalizedAsync(id)` · `WaitForTransitionAsync<TEvent>(from, to)` · `WaitForReplayAsync(id)`. Signal-driven; a timeout dumps the edges actually taken.
- Timeouts: `WaitForTimeoutAsync(name)` returns once it is parked on the transport; `FireTimeoutAsync(name)` then advances the clock exactly to its due time and returns when the saga has consumed it.
- Concurrency: `ConcurrencyConflicts(id)` · `Replays(id)` · `RecordedTransition.Attempt`.
- `Messaging` is the message harness underneath — `Published`, `Consumed`, `Faulted`, `DeadLettered` are forwarded.

### Outcomes are not interchangeable

`Transitioned` wrote state. `Conflicted` lost the version check — a `Transitioned` record at a higher `Attempt` follows, and *that* is the replay assertion; a conflict count stuck at zero in a test built to force one means the repository is not enforcing its contract. `Ignored` read the instance and wrote nothing — `FromState` is `null` only for a correlation miss, non-null for "no clause in this state" and for a dropped stale timeout. `Faulted` threw.

### Two things to know

- **The clock is always fake.** `Time` is a `FakeTimeProvider` so a timeout test is a condition, not a race. Because every SDK backoff sleeps on `TimeProvider`, the harness zeroes `SagaOptions.ConcurrencyRetryDelay` and the consume retry *backoff* (budgets untouched) — otherwise a delay nothing advances past never elapses. `configureSaga` / `configureBus` run after, so a test can put a real schedule back and advance `Time` itself.
- **A non-write outcome lands late.** `Ignored` / `Faulted` are only knowable when the message finishes, so they are recorded after the consume observers. Await `Transitions`, not `Consumed`, before asserting one.

### Blind spot

An instance created *and* finalized by one message under `RemoveOnFinalize` is never written — the coordinator skips an insert it would immediately delete — so it records as `Ignored`. Turn `RemoveOnFinalize` off in that test and the terminal write, and with it the transition, becomes visible.
