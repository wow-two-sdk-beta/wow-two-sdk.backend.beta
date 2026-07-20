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
