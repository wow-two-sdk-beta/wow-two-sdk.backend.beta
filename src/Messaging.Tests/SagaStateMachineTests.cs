using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Initiating event — correlates by order id.</summary>
public sealed record OrderPlaced(string OrderId, decimal Total) : IEvent;

/// <summary>Advancing event — correlates by the same order id.</summary>
public sealed record PaymentReceived(string OrderId, decimal Amount) : IEvent;

/// <summary>Published when a saga instance completes.</summary>
public sealed record OrderConfirmed(string OrderId, decimal Total) : IEvent;

/// <summary>The saga's own scheduled timeout — correlates by the envelope's correlation id.</summary>
public sealed record PaymentOverdue(string OrderId) : IEvent;

/// <summary>Published when the timeout fires instead of the payment.</summary>
public sealed record OrderCancelled(string OrderId) : IEvent;

/// <summary>Per-instance persisted state. <c>Interference</c> is written by a simulated concurrent writer, never by the machine.</summary>
public sealed class OrderSagaState : SagaState
{
    /// <summary>Copied off the initiating event — what a mis-correlated second event would visibly cross-contaminate.</summary>
    public decimal Total { get; set; }

    /// <summary>Bumped only by <see cref="ConflictOnceSagaRepository"/>, standing in for another process's committed write.</summary>
    public int Interference { get; set; }
}

/// <summary>Counts activity invocations across the whole host — saga state cannot, because a replay reloads it.</summary>
public sealed class SagaProbe
{
    private int _paymentActivityRuns;

    /// <summary>How many times the <see cref="PaymentReceived"/> transition's activities ran.</summary>
    public int PaymentActivityRuns => Volatile.Read(ref _paymentActivityRuns);

    /// <summary>Record one invocation.</summary>
    public void RecordPaymentActivity() => Interlocked.Increment(ref _paymentActivityRuns);
}

/// <summary>The machine under test: initiate → schedule a timeout → advance on payment, or cancel on the timeout.</summary>
public sealed class OrderStateMachine : SagaStateMachine<OrderSagaState>
{
    /// <summary>The one intermediate state.</summary>
    public const string AwaitingPayment = "awaiting-payment";

    /// <summary>How long the instance waits for payment before the scheduled timeout fires.</summary>
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(30);

    /// <summary>Declare the behaviour.</summary>
    public OrderStateMachine()
    {
        Initially(
            When<OrderPlaced>(e => e.OrderId)
                .Then(context => context.Saga.Total = context.Message.Total)
                .Schedule(PaymentWindow, context => new PaymentOverdue(context.CorrelationId))
                .TransitionTo(AwaitingPayment));

        During(
            AwaitingPayment,
            When<PaymentReceived>(e => e.OrderId)
                .Unschedule<PaymentOverdue>()
                .Then(context => context.Services.GetService<SagaProbe>()?.RecordPaymentActivity())
                .Publish(context => new OrderConfirmed(context.CorrelationId, context.Saga.Total + context.Message.Amount))
                .Finalize(),
            When<PaymentOverdue>()
                .Publish(context => new OrderCancelled(context.CorrelationId))
                .Finalize());
    }
}

/// <summary>
/// Loses the first <see cref="UpdateAsync"/> the way a real store does: another writer commits between this caller's
/// load and its write, so the version the caller holds is stale and the store rejects it.
/// </summary>
/// <remarks>
/// Everything else delegates to the shipped <see cref="InMemorySagaRepository{TState}"/>, so the conflict is produced by
/// the real optimistic-concurrency check rather than by a hand-thrown exception.
/// </remarks>
public sealed class ConflictOnceSagaRepository(TimeProvider timeProvider) : ISagaRepository<OrderSagaState>
{
    private readonly InMemorySagaRepository<OrderSagaState> _inner = new(timeProvider);
    private int _updates;

    /// <summary>Instances currently stored.</summary>
    public int Count => _inner.Count;

    /// <inheritdoc />
    public ValueTask<OrderSagaState?> LoadAsync(string correlationId, CancellationToken cancellationToken)
        => _inner.LoadAsync(correlationId, cancellationToken);

    /// <inheritdoc />
    public ValueTask InsertAsync(OrderSagaState state, CancellationToken cancellationToken)
        => _inner.InsertAsync(state, cancellationToken);

    /// <inheritdoc />
    public async ValueTask UpdateAsync(OrderSagaState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Interlocked.Increment(ref _updates) == 1)
        {
            var concurrent = await _inner.LoadAsync(state.CorrelationId, cancellationToken);
            if (concurrent is not null)
            {
                concurrent.Interference++;
                await _inner.UpdateAsync(concurrent, cancellationToken); // commits first; the caller's version is now stale
            }
        }

        await _inner.UpdateAsync(state, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(OrderSagaState state, CancellationToken cancellationToken)
        => _inner.DeleteAsync(state, cancellationToken);
}

/// <summary>The state-machine saga end to end: correlation, optimistic-concurrency replay, and a scheduled timeout.</summary>
public sealed class SagaStateMachineTests
{
    [Fact]
    public async Task Correlation_routes_each_key_to_its_own_instance()
    {
        await using var harness = await StartAsync();
        var repository = InstanceStore(harness);

        await harness.Bus.PublishAsync(new OrderPlaced("order-a", 100m));
        await harness.Bus.PublishAsync(new OrderPlaced("order-b", 55m));
        await harness.Consumed.WaitForAsync<OrderPlaced>(count: 2);

        repository.Count.Should().Be(2); // two keys, two instances

        // Same key as the first OrderPlaced: it has to find that instance, and the total it publishes is the proof —
        // a mis-correlation would carry order-b's 55 instead.
        await harness.Bus.PublishAsync(new PaymentReceived("order-a", 5m));

        var confirmed = await harness.Published.WaitForAsync<OrderConfirmed>();
        confirmed[0].BodyAs<OrderConfirmed>().Should().Be(new OrderConfirmed("order-a", 105m));

        await harness.Consumed.WaitForAsync<PaymentReceived>();
        repository.Count.Should().Be(1); // order-a finalized and removed; order-b untouched
        (await repository.LoadAsync("order-a", CancellationToken.None)).Should().BeNull();

        var untouched = await repository.LoadAsync("order-b", CancellationToken.None);
        untouched.Should().NotBeNull();
        untouched!.Total.Should().Be(55m);
        untouched.CurrentState.Should().Be(OrderStateMachine.AwaitingPayment);
    }

    [Fact]
    public async Task An_event_for_no_instance_is_ignored_rather_than_creating_one()
    {
        await using var harness = await StartAsync();

        // No Initially clause for PaymentReceived, so there is nothing to create and nothing to await — the assertion
        // is that the bus went quiet having stored nothing.
        await harness.Bus.PublishAsync(new PaymentReceived("order-ghost", 5m));
        await harness.WaitForIdleAsync();

        InstanceStore(harness).Count.Should().Be(0);
        harness.Published.Any<OrderConfirmed>().Should().BeFalse();
        harness.Faulted.Any().Should().BeFalse(); // SagaMissingInstance.Ignore is the default: dropped, not faulted
    }

    [Fact]
    public async Task A_version_conflict_replays_the_transition_instead_of_losing_it()
    {
        var probe = new SagaProbe();
        await using var harness = await StartAsync(
            services =>
            {
                services.AddSingleton(probe);
                services.AddSagaRepository<OrderSagaState, ConflictOnceSagaRepository>(ServiceLifetime.Singleton);
            },
            // Keeping the finalized instance is what makes the write an UpdateAsync (the version-checked path) and
            // leaves the converged state readable afterwards.
            configureSaga: o => o.RemoveOnFinalize = false);

        await harness.Bus.PublishAsync(new OrderPlaced("order-c", 100m));
        await harness.Consumed.WaitForAsync<OrderPlaced>();

        await harness.Bus.PublishAsync(new PaymentReceived("order-c", 5m));
        await harness.Consumed.WaitForAsync<PaymentReceived>();

        probe.PaymentActivityRuns.Should().Be(2); // lost the race once, re-ran against the reloaded state

        // Replay is at-least-once by construction — the SDK documents it as the price of never losing an update.
        harness.Published.Count<OrderConfirmed>().Should().Be(2);
        harness.Faulted.Any<PaymentReceived>().Should().BeFalse(); // resolved inside the saga, never escalated to retry

        var converged = await Repository(harness).LoadAsync("order-c", CancellationToken.None);
        converged.Should().NotBeNull();
        converged!.Interference.Should().Be(1); // the concurrent writer's commit survived
        converged.CurrentState.Should().Be(SagaStates.Final); // and this transition landed on top of it
        converged.FinalizedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task A_scheduled_timeout_fires_and_the_saga_reacts_to_it()
    {
        var time = new FakeTimeProvider();
        await using var harness = await StartAsync(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(time)));

        await harness.Bus.PublishAsync(new OrderPlaced("order-d", 100m));

        // The timeout is published with a delay, so the transport has parked it on the scheduler by the time this
        // returns — which is what makes the Advance below deterministic rather than a race against the schedule.
        var scheduled = await harness.Published.WaitForAsync<PaymentOverdue>();
        scheduled[0].Envelope.Headers.Should().ContainKey(SagaHeaders.TimeoutToken);
        scheduled[0].Envelope.NotBeforeUtc.Should().Be(time.GetUtcNow() + OrderStateMachine.PaymentWindow);
        harness.Published.Any<OrderCancelled>().Should().BeFalse(); // not yet due

        time.Advance(OrderStateMachine.PaymentWindow + TimeSpan.FromMinutes(1));

        await harness.Consumed.WaitForAsync<PaymentOverdue>();
        harness.Published.Bodies<OrderCancelled>().Should().ContainSingle(e => e.OrderId == "order-d");
        (await Repository(harness).LoadAsync("order-d", CancellationToken.None)).Should().BeNull(); // finalized by the timeout
    }

    private static ISagaRepository<OrderSagaState> Repository(MessagingTestHarness harness)
        => harness.Services.GetRequiredService<ISagaRepository<OrderSagaState>>();

    /// <summary>The default in-memory repository, for the instance count no interface member exposes.</summary>
    private static InMemorySagaRepository<OrderSagaState> InstanceStore(MessagingTestHarness harness)
        => (InMemorySagaRepository<OrderSagaState>)Repository(harness);

    private static Task<MessagingTestHarness> StartAsync(Action<IServiceCollection>? configureServices = null, Action<SagaOptions>? configureSaga = null)
        => MessagingTestHarness.StartAsync(
            services =>
            {
                services.AddSaga<OrderStateMachine, OrderSagaState>(configureSaga);
                configureServices?.Invoke(services);
            },
            handlerAssemblies: [typeof(OrderStateMachine).Assembly]);
}
