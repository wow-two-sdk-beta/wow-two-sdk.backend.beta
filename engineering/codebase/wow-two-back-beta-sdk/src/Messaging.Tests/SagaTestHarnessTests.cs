using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>State for the machine that insists a payment's instance already exists.</summary>
public sealed class StrictOrderSagaState : SagaState;

/// <summary>
/// Same flow as <see cref="OrderStateMachine"/>, but <see cref="PaymentReceived"/> declares
/// <see cref="SagaMissingInstance.Fault"/> — a payment for an order nobody placed means the events arrived out of
/// order, not that the flow already finished.
/// </summary>
public sealed class StrictOrderStateMachine : SagaStateMachine<StrictOrderSagaState>
{
    /// <summary>The one intermediate state.</summary>
    public const string AwaitingPayment = "awaiting-payment";

    /// <summary>Declare the behaviour.</summary>
    public StrictOrderStateMachine()
    {
        Initially(
            When<OrderPlaced>(e => e.OrderId)
                .TransitionTo(AwaitingPayment));

        During(
            AwaitingPayment,
            When<PaymentReceived>(e => e.OrderId)
                .IfMissing(SagaMissingInstance.Fault)
                .Finalize());
    }
}

/// <summary>State for the machine that keeps running after payment.</summary>
public sealed class ShipmentSagaState : SagaState;

/// <summary>
/// A flow that does <b>not</b> finalize on payment, so a cancelled timeout still has a live state to arrive into. That
/// is what makes the stale-token drop assertable: the <c>Paid</c> state declares a clause for
/// <see cref="PaymentOverdue"/>, so if the token check were skipped the timeout would cancel a paid order.
/// </summary>
public sealed class ShipmentStateMachine : SagaStateMachine<ShipmentSagaState>
{
    /// <summary>Waiting for the payment that was scheduled against a timeout.</summary>
    public const string AwaitingPayment = "awaiting-payment";

    /// <summary>Paid — still running, and still bound to the timeout it cancelled.</summary>
    public const string Paid = "paid";

    /// <summary>How long the instance waits for payment.</summary>
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(30);

    /// <summary>Declare the behaviour.</summary>
    public ShipmentStateMachine()
    {
        Initially(
            When<OrderPlaced>(e => e.OrderId)
                .Schedule(PaymentWindow, context => new PaymentOverdue(context.CorrelationId))
                .TransitionTo(AwaitingPayment));

        During(
            AwaitingPayment,
            When<PaymentReceived>(e => e.OrderId)
                .Unschedule<PaymentOverdue>()
                .TransitionTo(Paid),
            When<PaymentOverdue>()
                .Publish(context => new OrderCancelled(context.CorrelationId))
                .Finalize());

        // The trap: a live clause for the cancelled timeout. Only the token check stops it firing.
        During(
            Paid,
            When<PaymentOverdue>()
                .Publish(context => new OrderCancelled(context.CorrelationId))
                .Finalize());
    }
}

/// <summary>
/// The saga harness itself: two of <see cref="SagaStateMachineTests"/>'s cases ported onto it, plus the paths that were
/// invisible without it — the correlation miss under both missing-instance policies, retention after finalization, and
/// a timeout that arrives after being unscheduled.
/// </summary>
public sealed class SagaTestHarnessTests
{
    [Fact]
    public async Task Correlation_routes_each_key_to_its_own_instance()
    {
        await using var harness = await SagaTestHarness.StartAsync<OrderStateMachine, OrderSagaState>();

        await harness.Bus.PublishAsync(new OrderPlaced("order-a", 100m));
        await harness.Bus.PublishAsync(new OrderPlaced("order-b", 55m));

        // The direct condition, not a proxy for it: each key reached its own state, so each key has its own instance.
        await harness.WaitForStateAsync("order-a", OrderStateMachine.AwaitingPayment);
        await harness.WaitForStateAsync("order-b", OrderStateMachine.AwaitingPayment);
        harness.CountInstances().Should().Be(2);

        // Same key as the first OrderPlaced: it has to find that instance, and the total it publishes is the proof —
        // a mis-correlation would carry order-b's 55 instead.
        await harness.Bus.PublishAsync(new PaymentReceived("order-a", 5m));
        var finalized = await harness.WaitForFinalizedAsync("order-a");

        finalized.FromState.Should().Be(OrderStateMachine.AwaitingPayment); // awaiting-payment --PaymentReceived--> final
        finalized.Is<PaymentReceived>().Should().BeTrue();
        finalized.Removed.Should().BeTrue(); // RemoveOnFinalize is on by default

        harness.Published.Bodies<OrderConfirmed>().Should().ContainSingle()
            .Which.Should().Be(new OrderConfirmed("order-a", 105m));

        harness.CountInstances().Should().Be(1); // order-a removed; order-b untouched
        (await harness.GetInstanceAsync("order-a")).Should().BeNull();

        var untouched = await harness.GetInstanceAsync("order-b");
        untouched.Should().NotBeNull();
        untouched!.Total.Should().Be(55m);
        untouched.CurrentState.Should().Be(OrderStateMachine.AwaitingPayment);
        harness.HasTransition<PaymentReceived>(correlationId: "order-b").Should().BeFalse();
    }

    [Fact]
    public async Task A_version_conflict_replays_the_transition_instead_of_losing_it()
    {
        var probe = new SagaProbe();
        await using var harness = await SagaTestHarness.StartAsync<OrderStateMachine, OrderSagaState>(
            configureServices: services => services.AddSingleton(probe),
            // Keeping the finalized instance is what makes the write an UpdateAsync (the version-checked path) and
            // leaves the converged state readable afterwards.
            configureSaga: options => options.RemoveOnFinalize = false,
            repository: provider => new ConflictOnceSagaRepository(provider.GetRequiredService<TimeProvider>()));

        await harness.Bus.PublishAsync(new OrderPlaced("order-c", 100m));
        await harness.WaitForStateAsync("order-c", OrderStateMachine.AwaitingPayment);

        await harness.Bus.PublishAsync(new PaymentReceived("order-c", 5m));
        var replayed = await harness.WaitForReplayAsync("order-c");

        harness.ConcurrencyConflicts("order-c").Should().Be(1); // the store rejected the stale write, as its contract requires
        replayed[0].Attempt.Should().Be(2); // and the coordinator re-ran the transition against reloaded state
        replayed[0].ToState.Should().Be(SagaStates.Final);
        probe.PaymentActivityRuns.Should().Be(2);

        // Replay is at-least-once by construction — the SDK documents it as the price of never losing an update.
        harness.Published.Count<OrderConfirmed>().Should().Be(2);
        harness.Faulted.Any<PaymentReceived>().Should().BeFalse(); // resolved inside the saga, never escalated to retry

        var converged = await harness.GetInstanceAsync("order-c");
        converged.Should().NotBeNull();
        converged!.Interference.Should().Be(1); // the concurrent writer's commit survived
        converged.CurrentState.Should().Be(SagaStates.Final); // and this transition landed on top of it
        converged.FinalizedAtUtc.Should().Be(harness.Time.GetUtcNow());
    }

    [Fact]
    public async Task A_scheduled_timeout_fires_and_the_saga_reacts_to_it()
    {
        await using var harness = await SagaTestHarness.StartAsync<OrderStateMachine, OrderSagaState>();

        await harness.Bus.PublishAsync(new OrderPlaced("order-d", 100m));

        var scheduled = await harness.WaitForTimeoutAsync();
        scheduled.Name.Should().Be(nameof(PaymentOverdue));
        scheduled.CorrelationId.Should().Be("order-d");
        scheduled.DueUtc.Should().Be(harness.Time.GetUtcNow() + OrderStateMachine.PaymentWindow);
        harness.Published.Any<OrderCancelled>().Should().BeFalse(); // not yet due

        // Waits for the timeout to be parked, advances exactly to its due time, returns when the saga consumed it.
        await harness.FireTimeoutAsync();

        var cancelled = await harness.WaitForFinalizedAsync("order-d");
        cancelled.Is<PaymentOverdue>().Should().BeTrue();
        cancelled.FromState.Should().Be(OrderStateMachine.AwaitingPayment);
        harness.Published.Bodies<OrderCancelled>().Should().ContainSingle(e => e.OrderId == "order-d");
        (await harness.GetInstanceAsync("order-d")).Should().BeNull(); // finalized by the timeout
    }

    [Fact]
    public async Task A_correlation_miss_is_recorded_as_ignored_rather_than_dropped_silently()
    {
        await using var harness = await SagaTestHarness.StartAsync<OrderStateMachine, OrderSagaState>();

        // No Initially clause for PaymentReceived, so nothing is created — and without the harness the only assertion
        // available is a negative one. The transition log turns the non-event into a positive fact.
        await harness.Bus.PublishAsync(new PaymentReceived("order-ghost", 5m));

        // On the transition, not on the delivery: a saga that writes nothing is only recorded once the message is done.
        await harness.Transitions.WaitForAsync(transition => transition.Is<PaymentReceived>(), what: "the ignored payment");
        await harness.WaitForIdleAsync();

        var ignored = harness.Transitions.All.Should().ContainSingle().Subject;
        ignored.Outcome.Should().Be(SagaTransitionOutcome.Ignored);
        ignored.CorrelationId.Should().Be("order-ghost");
        ignored.FromState.Should().BeNull(); // null from-state is the correlation miss specifically — no instance existed
        ignored.Is<PaymentReceived>().Should().BeTrue();

        harness.CountInstances().Should().Be(0);
        harness.Published.Any<OrderConfirmed>().Should().BeFalse();
        harness.Faulted.Any().Should().BeFalse(); // SagaMissingInstance.Ignore is the default: dropped, not faulted
        harness.DeadLettered.Any().Should().BeFalse();
    }

    [Fact]
    public async Task A_correlation_miss_faults_the_message_when_the_clause_demands_an_instance()
    {
        await using var harness = await SagaTestHarness.StartAsync<StrictOrderStateMachine, StrictOrderSagaState>(
            configureBus: options => options.Retry = new RetryConfig(MaxAttempts: 2, Backoff: BackoffKind.None));

        await harness.Bus.PublishAsync(new PaymentReceived("order-ghost", 5m));
        await harness.DeadLettered.WaitForAsync<PaymentReceived>();

        // Same event, same missing instance, opposite disposition to the Ignore case above.
        var faulted = harness.Transitions.All.Should().ContainSingle().Subject;
        faulted.Outcome.Should().Be(SagaTransitionOutcome.Faulted);
        faulted.FromState.Should().BeNull();
        faulted.Exception.Should().BeOfType<InvalidOperationException>();

        harness.Faulted.Count<PaymentReceived>().Should().Be(2); // took the ordinary retry budget, then dead-lettered
        harness.CountInstances().Should().Be(0); // faulting is not creating
    }

    [Fact]
    public async Task A_finalized_instance_is_retained_until_a_purge_sweeps_it()
    {
        await using var harness = await SagaTestHarness.StartAsync<OrderStateMachine, OrderSagaState>(
            configureSaga: options => options.RemoveOnFinalize = false);

        await harness.Bus.PublishAsync(new OrderPlaced("order-r", 40m));
        await harness.WaitForStateAsync("order-r", OrderStateMachine.AwaitingPayment);

        await harness.Bus.PublishAsync(new PaymentReceived("order-r", 10m));
        var finalized = await harness.WaitForFinalizedAsync("order-r");

        finalized.Removed.Should().BeFalse(); // retained for inspection, not deleted
        finalized.Instance!.FinalizedAtUtc.Should().Be(harness.Time.GetUtcNow());
        (await harness.CurrentStateAsync("order-r")).Should().Be(SagaStates.Final);
        harness.CountInstances().Should().Be(1);

        // Retention is measured from FinalizedAtUtc, so a sweep with a window the instance has not outlived keeps it.
        (await harness.PurgeFinalizedAsync(TimeSpan.FromHours(1))).Should().Be(0);
        harness.CountInstances().Should().Be(1);

        harness.Time.Advance(TimeSpan.FromHours(2));
        (await harness.PurgeFinalizedAsync(TimeSpan.FromHours(1))).Should().Be(1);
        harness.CountInstances().Should().Be(0);
    }

    [Fact]
    public async Task An_unscheduled_timeout_is_dropped_when_the_transport_delivers_it_anyway()
    {
        await using var harness = await SagaTestHarness.StartAsync<ShipmentStateMachine, ShipmentSagaState>(
            configureSaga: options => options.RemoveOnFinalize = false);

        await harness.Bus.PublishAsync(new OrderPlaced("ship-1", 20m));
        var scheduled = await harness.WaitForTimeoutAsync(correlationId: "ship-1");

        // Unschedule forgets the token; the message itself cannot be recalled from a transport that accepted it.
        await harness.Bus.PublishAsync(new PaymentReceived("ship-1", 20m));
        var paid = await harness.WaitForStateAsync("ship-1", ShipmentStateMachine.Paid);
        paid.Instance!.TimeoutTokens.Should().NotContainKey(nameof(PaymentOverdue));

        await harness.FireTimeoutAsync(correlationId: "ship-1");

        // It arrived into a state that declares a clause for it — only the token mismatch stopped the transition.
        var dropped = harness.Transitions.For("ship-1").Should().HaveCount(3).And.Subject.Last();
        dropped.Is<PaymentOverdue>().Should().BeTrue();
        dropped.Outcome.Should().Be(SagaTransitionOutcome.Ignored);
        dropped.FromState.Should().Be(ShipmentStateMachine.Paid); // a live instance, not a correlation miss
        dropped.Envelope!.Headers[SagaHeaders.TimeoutToken].Should().Be(scheduled.Token);

        harness.Published.Any<OrderCancelled>().Should().BeFalse(); // the paid order was not cancelled
        (await harness.CurrentStateAsync("ship-1")).Should().Be(ShipmentStateMachine.Paid);
    }
}
