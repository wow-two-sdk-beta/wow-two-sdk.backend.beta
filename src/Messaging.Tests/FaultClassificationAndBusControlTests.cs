using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Exception classification (retry / dead-letter / ignore) and runtime bus control (pause / resume / stop).</summary>
public sealed class FaultClassificationAndBusControlTests
{
    private static IHost BuildHost(Action<IServiceCollection>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<EventCollector>();
        builder.Services.AddSingleton(new ConcurrencyProbe { Hold = TimeSpan.FromMilliseconds(20) });
        builder.Services.AddInMemoryEventBus(
            o => o.Retry = new RetryConfig(MaxAttempts: 5, Backoff: BackoffKind.None),
            typeof(PingHandler).Assembly);

        configure?.Invoke(builder.Services);
        return builder.Build();
    }

    private static async Task<DeadLetterRecord?> WaitForDeadLetterAsync(IDeadLetterStore store, string source, string messageId, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            await foreach (var record in store.ReadAsync(source, CancellationToken.None))
                if (record.MessageId == messageId)
                    return record;

            await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        }

        return null;
    }

    [Fact]
    public async Task Dead_letters_a_non_retryable_exception_without_burning_the_retry_budget()
    {
        using var host = BuildHost(s => s.AddEventFaultClassification(r => r.DeadLetterOn<InvalidOperationException>()));
        await host.StartAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        var deadLetters = host.Services.GetRequiredService<IDeadLetterStore>();
        await bus.PublishAsync(new BoomEvent("x"), new PublishOptions { MessageId = "nonretryable-1" });

        // MaxAttempts is 5 with no backoff; a classified-fatal fault must land in the DLQ on the first failure.
        var record = await WaitForDeadLetterAsync(deadLetters, nameof(BoomEvent), "nonretryable-1", TimeSpan.FromSeconds(5));
        record.Should().NotBeNull();
        record!.ExceptionType.Should().Contain(nameof(InvalidOperationException));
        await host.StopAsync();
    }

    [Fact]
    public async Task Acknowledges_an_ignored_exception_instead_of_dead_lettering()
    {
        using var host = BuildHost(s => s.AddEventFaultClassification(r => r.IgnoreOn<InvalidOperationException>()));
        await host.StartAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        var deadLetters = host.Services.GetRequiredService<IDeadLetterStore>();
        await bus.PublishAsync(new BoomEvent("x"), new PublishOptions { MessageId = "ignored-1" });

        // Ignore = treat as handled: the resilience pipeline swallows, the pipeline's success path acknowledges.
        var record = await WaitForDeadLetterAsync(deadLetters, nameof(BoomEvent), "ignored-1", TimeSpan.FromSeconds(2));
        record.Should().BeNull();
        await host.StopAsync();
    }

    [Fact]
    public async Task Retries_an_unclassified_exception_as_before()
    {
        using var host = BuildHost(); // no rules registered — default must be today's behaviour
        await host.StartAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        var deadLetters = host.Services.GetRequiredService<IDeadLetterStore>();
        await bus.PublishAsync(new BoomEvent("x"), new PublishOptions { MessageId = "retried-1" });

        var record = await WaitForDeadLetterAsync(deadLetters, nameof(BoomEvent), "retried-1", TimeSpan.FromSeconds(5));
        record.Should().NotBeNull(); // still dead-lettered, but only after the retry budget is spent
        await host.StopAsync();
    }

    [Fact]
    public async Task Pause_holds_messages_at_the_gate_and_resume_releases_them()
    {
        using var host = BuildHost();
        await host.StartAsync();

        var control = host.Services.GetRequiredService<IBusControl>();
        var collector = host.Services.GetRequiredService<EventCollector>();
        var bus = host.Services.GetRequiredService<IEventBus>();

        control.State.Should().Be(BusState.Running);
        await control.PauseAsync();
        control.State.Should().Be(BusState.Paused);

        for (var i = 0; i < 3; i++)
            await bus.PublishAsync(new PingEvent($"p{i}"));

        // Paused gates entry into the pipeline; nothing should be handled while the gate is shut.
        await Task.Delay(TimeSpan.FromMilliseconds(300), CancellationToken.None);
        collector.Count.Should().Be(0);

        await control.ResumeAsync();
        control.State.Should().Be(BusState.Running);

        (await collector.WaitForCountAsync(3, TimeSpan.FromSeconds(5))).Should().BeTrue();
        await host.StopAsync();
    }

    [Fact]
    public async Task Stop_drains_and_reports_stopped()
    {
        using var host = BuildHost();
        await host.StartAsync();

        var control = host.Services.GetRequiredService<IBusControl>();
        var bus = host.Services.GetRequiredService<IEventBus>();
        var collector = host.Services.GetRequiredService<EventCollector>();

        await bus.PublishAsync(new PingEvent("x"));
        (await collector.WaitForCountAsync(1, TimeSpan.FromSeconds(5))).Should().BeTrue();

        await control.StopAsync();
        control.State.Should().Be(BusState.Stopped);
        control.InFlight.Should().Be(0);
        await host.StopAsync();
    }
}
