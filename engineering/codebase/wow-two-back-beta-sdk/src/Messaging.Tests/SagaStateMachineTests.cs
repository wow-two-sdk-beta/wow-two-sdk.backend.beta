using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>The state-machine saga end to end: correlation, optimistic-concurrency replay, and a scheduled timeout.</summary>
public sealed class SagaStateMachineTests
{
    [Fact]
    public async Task Correlation_routes_each_key_to_its_own_instance()
    {
        await using var harness = await StartAsync();
        var repository = InstanceStore(harness);

        await harness.Bus.PublishAsync(new OrderPlaced { OrderId = "order-a", Total = 100m });
        await harness.Bus.PublishAsync(new OrderPlaced { OrderId = "order-b", Total = 55m });
        await harness.Consumed.WaitForAsync<OrderPlaced>(count: 2);

        repository.Count.Should().Be(2); // two keys, two instances

        // Same key as the first OrderPlaced: it has to find that instance, and the total it publishes is the proof —
        // a mis-correlation would carry order-b's 55 instead.
        await harness.Bus.PublishAsync(new PaymentReceived { OrderId = "order-a", Amount = 5m });

        var confirmed = await harness.Published.WaitForAsync<OrderConfirmed>();
        confirmed[0].BodyAs<OrderConfirmed>().Should().Be(new OrderConfirmed { OrderId = "order-a", Total = 105m });

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
        await harness.Bus.PublishAsync(new PaymentReceived { OrderId = "order-ghost", Amount = 5m });
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

        await harness.Bus.PublishAsync(new OrderPlaced { OrderId = "order-c", Total = 100m });
        await harness.Consumed.WaitForAsync<OrderPlaced>();

        await harness.Bus.PublishAsync(new PaymentReceived { OrderId = "order-c", Amount = 5m });
        await harness.Consumed.WaitForAsync<PaymentReceived>();

        probe.PaymentActivityRuns.Should().Be(2); // lost the race once, re-ran against the reloaded state

        // Replay is at-least-once by construction — the SDK documents it as the price of never losing an update.
        harness.Published.Count<OrderConfirmed>().Should().Be(2);
        harness.Faulted.Any<PaymentReceived>().Should().BeFalse(); // resolved inside the saga, never escalated to retry

        var converged = await Repository(harness).LoadAsync("order-c", CancellationToken.None);
        converged.Should().NotBeNull();
        converged!.Interference.Should().Be(1); // the concurrent writer's commit survived
        converged.CurrentState.Should().Be(SagaStateConstants.Final); // and this transition landed on top of it
        converged.FinalizedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task A_scheduled_timeout_fires_and_the_saga_reacts_to_it()
    {
        var time = new FakeTimeProvider();
        await using var harness = await StartAsync(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(time)));

        await harness.Bus.PublishAsync(new OrderPlaced { OrderId = "order-d", Total = 100m });

        // The timeout is published with a delay, so the transport has parked it on the scheduler by the time this
        // returns — which is what makes the Advance below deterministic rather than a race against the schedule.
        var scheduled = await harness.Published.WaitForAsync<PaymentOverdue>();
        scheduled[0].Envelope.Headers.Should().ContainKey(SagaHeaderConstants.TimeoutToken);
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
                services.AddScannedHandlerDependencies();
                services.AddSaga<OrderStateMachine, OrderSagaState>(configureSaga);
                configureServices?.Invoke(services);
            },
            handlerAssemblies: [typeof(OrderStateMachine).Assembly]);
}
