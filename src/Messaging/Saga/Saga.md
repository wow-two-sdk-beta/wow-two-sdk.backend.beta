# Messaging.Saga

State-machine sagas: **correlated, persisted** process state that lives *between* messages. Sibling of the
routing-slip runner in [`../EventSaga/`](../EventSaga/EventSaga.cs) — both stay, they answer different questions.

| | routing slip (`EventSaga/`) | state machine (`Saga/`) |
|---|---|---|
| shape | ordered itinerary this process drives | states + events the world drives |
| state | in memory, one call | `ISagaRepository<TState>`, between messages |
| failure | compensate completed steps in reverse | ordinary retry / dead-letter per message |
| survives a crash | no | yes, with a durable repository |
| waits | no | yes — timeouts / schedule-to-self |

## Model

```
event ──▶ correlate (key from body, or envelope correlation id)
      ──▶ ISagaRepository.LoadAsync           (null ⇒ only an `Initially` clause may create)
      ──▶ clause for CurrentState, else DuringAny, else ignore
      ──▶ activities (Then / Publish / Schedule / Unschedule)
      ──▶ CurrentState := target | final
      ──▶ Insert / Update / Delete            (version-checked)
      ──▶ flush scheduled timeouts            (only after the write commits)
```

## Wire-up

```csharp
public sealed class OrderSagaState : SagaState          // CorrelationId · CurrentState · Version · timeout tokens
{
    public decimal Total { get; set; }
}

public sealed class OrderStateMachine : SagaStateMachine<OrderSagaState>
{
    public const string AwaitingPayment = "awaiting-payment";

    public OrderStateMachine()
    {
        Initially(
            When<OrderPlaced>(e => e.OrderId)                        // correlation: once per event type
                .Then(ctx => ctx.Saga.Total = ctx.Message.Total)
                .Schedule(TimeSpan.FromMinutes(30), ctx => new PaymentOverdue(ctx.CorrelationId))
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When<PaymentReceived>(e => e.OrderId)
                .Unschedule<PaymentOverdue>()
                .Publish(ctx => new OrderConfirmed(ctx.CorrelationId))
                .Finalize(),
            When<PaymentOverdue>()                                   // self-sent ⇒ correlates by envelope
                .Publish(ctx => new OrderCancelled(ctx.CorrelationId))
                .Finalize());

        DuringAny(
            When<OrderCancelledByCustomer>(e => e.OrderId).Finalize());
    }
}
```

```csharp
services.AddInMemoryEventBus(typeof(Program).Assembly);
services.AddSaga<OrderStateMachine, OrderSagaState>();               // BEFORE AddMessageTopology
services.AddSagaRepository<OrderSagaState, EfOrderSagaRepository>(); // optional — in-memory otherwise
services.AddMessageTopology();
```

**Correlation is declared once per event type**, not per clause — it has to run before the instance, and so its
state, is known. Put the selector on the type's first clause and use the bare `When<T>()` on the rest; a second
selector for the same type throws at startup. A type the machine schedules itself needs none.

A saga consumes through ordinary `IEventHandler<T>` registrations, so it inherits the pump, consume filters,
retry, dead-lettering and metrics unchanged — and shows up in the consumed-type set the broker topology binds.
Register it **before** `AddMessageTopology`, or its events get no binding.

## Concurrency — optimistic, resolved by replay

Two messages for one instance can be processed at once (the pump runs N workers), so a transition is a
read-modify-write that can lose.

- `ISagaRepository` owns it: insert fails on a duplicate key, update/delete fail on a version mismatch, both with
  `SagaConcurrencyException`. The in-memory default implements the same contract, not a simplified one.
- The coordinator answers a conflict by **reloading and re-running the transition**, up to
  `SagaOptions.MaxConcurrencyRetries`; after that the exception escapes into the normal retry / dead-letter path.
- Consequence: **an activity can run twice for one message** — keep activities idempotent, which at-least-once
  delivery already demands.
- **Avoid the race instead:** set `PartitionKey` = the saga's correlation key on every message that drives it. The
  pump hashes it onto one worker, so an instance's messages are serialized in arrival order. Timeouts and anything
  published via `ctx.PublishAsync` already carry it.

Pessimistic locking was rejected: the lock would have to span arbitrary user activities, turning a slow call into a
stalled partition and a database lock's lifetime into a handler's problem.

## Timeouts

`.Schedule<TTimeout>(delay, factory)` publishes the timeout **by type** after the state write commits.

- delivery: transport delay (`NativeDelay`/`NativeScheduling` → `IEventScheduler` on the in-memory path) → a
  registered `IEventScheduler` → an in-process timer, logged once at startup as non-durable.
- `.Unschedule<TTimeout>()` cannot recall an accepted message. The instance forgets the token it minted
  (`ISagaState.TimeoutTokens`, persisted); an arriving timeout whose token no longer matches is dropped.

## Post-B4 routing

Timeouts and `ctx.PublishAsync` are **published by type**, never sent to an ad-hoc destination — the one addressing
shape guaranteed to hit a bound routing key now the RabbitMQ `#` catch-all is gone and publishes are
`mandatory: false`. The routing-slip transport, which does take a destination, now checks it against the local
topology and logs an unbound one once (`EventSagaRunner.cs`).

## Types

| Type | Role |
|---|---|
| `ISagaState` / `SagaState` | correlation id · current state · version · finalized-at · timeout tokens · `Copy()` |
| `ISagaRepository<TState>` | load / insert / update / delete / purge-finalized, version-checked |
| `InMemorySagaRepository<TState>` | default; stores copies, `TryAdd` insert, compare-and-swap update |
| `SagaStateMachine<TState>` | `Initially` · `During` · `DuringAny` · `When` → `Then`/`Publish`/`Schedule`/`TransitionTo`/`Finalize` |
| `SagaTransitionContext<TState,TEvent>` | instance · message · DI scope · publish · branch · schedule/cancel timeout |
| `SagaCoordinator<TState>` | correlate → load → transition → write, with concurrency replay |
| `SagaOptions` | concurrency retries · remove-on-finalize |
| `AddSaga<TMachine,TState>()` | DI registration (machine + repository + one handler per observed event) |

## NEXT

- Durable repositories (EF / Mongo / Redis) — abstraction only here, per the provider-breadth rule.
- Event-sourced instances · saga versioning · query/inspection surface · saga observers · state-machine compensation.
- A hosted sweeper calling `PurgeFinalizedAsync` when `RemoveOnFinalize` is off.

## See also

- Routing slip: [`../EventSaga/EventSaga.cs`](../EventSaga/EventSaga.cs) · scheduler port:
  [`../Reliability/MessagingReliability.cs`](../Reliability/MessagingReliability.cs) · ordering:
  [`../Transport/MessagePump.cs`](../Transport/MessagePump.cs) · topology: [`../Transport/Topology.cs`](../Transport/Topology.cs)
- [`../Messaging.md`](../Messaging.md) · backlog §3.7:
  [`../../docs/planning/messaging/events-maturity-backlog.md`](../../docs/planning/messaging/events-maturity-backlog.md)
