using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>The shipped harness itself: what each log records, and that the waits are conditions rather than sleeps.</summary>
public sealed class MessagingTestHarnessTests
{
    [Fact]
    public async Task Records_both_halves_of_a_round_trip()
    {
        await using var harness = await MessagingTestHarness.StartAsync(handlerAssemblies: [typeof(HarnessHandler).Assembly]);

        await harness.Bus.PublishAsync(new HarnessEvent("round-trip"), new PublishOptions { MessageId = "rt-1" });

        var consumed = await harness.Consumed.WaitForAsync<HarnessEvent>(match: e => e.Tag == "round-trip");
        consumed[0].MessageId.Should().Be("rt-1");
        consumed[0].Outcome.Should().Be(ConsumeOutcome.Success);
        consumed[0].Destination.Should().Be(nameof(HarnessEvent));

        harness.Published.Count<HarnessEvent>(e => e.Tag == "round-trip").Should().Be(1);
        harness.Published.Bodies<HarnessEvent>().Should().ContainSingle(e => e.Tag == "round-trip");
        harness.Faulted.Any().Should().BeFalse();
        harness.DeadLettered.Any().Should().BeFalse();
    }

    [Fact]
    public async Task Faulted_holds_one_entry_per_delivery_attempt()
    {
        await using var harness = await MessagingTestHarness.StartAsync(
            configureBus: o => o.Retry = new RetryConfig(MaxAttempts: 3, Backoff: BackoffKind.None),
            handlerAssemblies: [typeof(HarnessHandler).Assembly]);

        await harness.Bus.PublishAsync(new BoomEvent("x"), new PublishOptions { MessageId = "attempts-1" });

        await harness.DeadLettered.WaitForAsync<BoomEvent>();

        // The distinction the old collector could not make: a spent retry budget looks nothing like a fault the
        // classifier ruled non-retryable, and only the per-attempt count separates them.
        harness.Faulted.Count<BoomEvent>().Should().Be(3);
        harness.DeadLettered.Count<BoomEvent>().Should().Be(1);
        harness.Consumed.Any<BoomEvent>().Should().BeFalse(); // no attempt ever completed
    }

    [Fact]
    public async Task Idle_wait_returns_while_the_bus_is_paused_and_holding_messages()
    {
        await using var harness = await MessagingTestHarness.StartAsync(handlerAssemblies: [typeof(HarnessHandler).Assembly]);
        harness.Control.Should().NotBeNull();
        var control = harness.Control!;

        await control.PauseAsync();

        for (var i = 0; i < 3; i++)
            await harness.Bus.PublishAsync(new HarnessEvent($"paused-{i}"));

        // Nothing will ever be consumed while the gate is shut, so there is no message to await — the assertion is
        // that the bus went quiet with the messages still parked.
        await harness.WaitForIdleAsync();
        harness.Published.Count<HarnessEvent>().Should().Be(3);
        harness.Consumed.Any<HarnessEvent>().Should().BeFalse();

        await control.ResumeAsync();

        await harness.Consumed.WaitForAsync<HarnessEvent>(count: 3);
    }

    [Fact]
    public async Task In_flight_wait_blocks_until_the_handler_lands()
    {
        var gate = new HarnessGate { Hold = true };
        await using var harness = await MessagingTestHarness.StartAsync(
            services => services.AddSingleton(gate),
            handlerAssemblies: [typeof(HarnessHandler).Assembly]);

        await harness.Bus.PublishAsync(new HarnessEvent("held"));
        await gate.Started.WaitAsync(TimeSpan.FromSeconds(5)); // parked inside the pipeline, not merely published

        harness.InFlight.Should().Be(1);

        var idle = harness.WaitForInFlightZeroAsync();
        idle.IsCompleted.Should().BeFalse(); // the point of the primitive: it does not return over in-flight work

        gate.Release();
        await idle;

        harness.InFlight.Should().Be(0);
        gate.Completed.Should().Be(1);
    }

    [Fact]
    public async Task Attaches_to_a_host_the_test_built_itself()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddInMemoryEventBus(typeof(HarnessHandler).Assembly);
        builder.Services.AddMessagingRecorder(); // the seam a WebApplicationFactory or broker-backed host uses
        using var host = builder.Build();
        await host.StartAsync();

        var harness = MessagingTestHarness.Attach(host.Services);
        await harness.Bus.PublishAsync(new HarnessEvent("attached"));
        await harness.Consumed.WaitForAsync<HarnessEvent>(match: e => e.Tag == "attached");

        await harness.DisposeAsync();
        host.Services.GetRequiredService<IBusControl>().State.Should().Be(BusState.Running); // attached harness owns nothing

        await host.StopAsync();
    }

    [Fact]
    public async Task A_timed_out_wait_reports_what_it_actually_saw()
    {
        await using var harness = await MessagingTestHarness.StartAsync(handlerAssemblies: [typeof(HarnessHandler).Assembly]);

        await harness.Bus.PublishAsync(new HarnessEvent("only-one"));
        await harness.Consumed.WaitForAsync<HarnessEvent>();

        Func<Task> waitForTwo = async () => await harness.Consumed.WaitForAsync<HarnessEvent>(count: 2, timeout: TimeSpan.FromMilliseconds(200));

        // A bare false makes for a useless assertion failure; the census is the whole reason these waits throw.
        (await waitForTwo.Should().ThrowAsync<TimeoutException>()).WithMessage("*saw 1*HarnessEvent/Success x1*");
    }
}
