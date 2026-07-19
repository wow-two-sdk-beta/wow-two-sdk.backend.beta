using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>An event whose handler holds a slot long enough for the pump's concurrency to be observable.</summary>
public sealed record SlowEvent(string Tag) : IEvent;

/// <summary>Records peak overlap and per-key arrival order across handler invocations.</summary>
public sealed class ConcurrencyProbe
{
    private readonly ConcurrentQueue<string> _started = new();
    private readonly ConcurrentQueue<string> _completed = new();
    private int _current;
    private int _peak;

    /// <summary>How long each handler holds its slot.</summary>
    public TimeSpan Hold { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>The highest number of handlers observed running at the same instant.</summary>
    public int Peak => Volatile.Read(ref _peak);

    /// <summary>Tags in the order handlers began, which is dispatch order.</summary>
    public IReadOnlyCollection<string> Started => _started;

    /// <summary>Tags of handlers that ran to completion.</summary>
    public IReadOnlyCollection<string> Completed => _completed;

    /// <summary>Run one handler body: record entry, hold the slot, record completion.</summary>
    /// <param name="tag">The event's tag.</param>
    public async Task RunAsync(string tag)
    {
        _started.Enqueue(tag);
        var now = Interlocked.Increment(ref _current);
        var peak = Volatile.Read(ref _peak);
        while (now > peak && Interlocked.CompareExchange(ref _peak, now, peak) != peak)
            peak = Volatile.Read(ref _peak);

        try
        {
            await Task.Delay(Hold, CancellationToken.None);
            _completed.Enqueue(tag);
        }
        finally
        {
            Interlocked.Decrement(ref _current);
        }
    }
}

/// <summary>Feeds every <see cref="SlowEvent"/> through the probe.</summary>
public sealed class SlowHandler(ConcurrencyProbe probe) : IEventHandler<SlowEvent>
{
    public async ValueTask HandleAsync(EventContext<SlowEvent> context, CancellationToken cancellationToken)
        => await probe.RunAsync(context.Event.Tag);
}

/// <summary>The concurrency pump: sequential by default, parallel when configured, ordered within a partition key, drained on stop.</summary>
public sealed class MessagePumpTests
{
    private static IHost BuildHost(Action<ConcurrencyOptions>? concurrency, ConcurrencyProbe probe)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<EventCollector>();
        builder.Services.AddInMemoryEventBus(typeof(SlowHandler).Assembly);
        if (concurrency is not null)
            builder.Services.AddMessagingConcurrency(concurrency);

        return builder.Build();
    }

    [Fact]
    public async Task Dispatches_sequentially_by_default()
    {
        var probe = new ConcurrencyProbe { Hold = TimeSpan.FromMilliseconds(80) };
        using var host = BuildHost(concurrency: null, probe);
        await host.StartAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        for (var i = 0; i < 6; i++)
            await bus.PublishAsync(new SlowEvent($"m{i}"));

        await WaitForCompletionAsync(probe, 6, TimeSpan.FromSeconds(10));
        await host.StopAsync();

        probe.Completed.Should().HaveCount(6);
        probe.Peak.Should().Be(1); // no pump configured — dispatch stays inline on the consume loop, as before the pump existed
    }

    [Fact]
    public async Task Processes_messages_in_parallel_when_concurrency_is_raised()
    {
        var probe = new ConcurrencyProbe { Hold = TimeSpan.FromMilliseconds(150) };
        using var host = BuildHost(o => o.MaxConcurrentMessages = 4, probe);
        await host.StartAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        for (var i = 0; i < 8; i++)
            await bus.PublishAsync(new SlowEvent($"m{i}")); // no partition key — round-robin across all 4 workers

        await WaitForCompletionAsync(probe, 8, TimeSpan.FromSeconds(15));
        await host.StopAsync();

        probe.Completed.Should().HaveCount(8);
        probe.Peak.Should().BeGreaterThan(1); // the whole point: the consume loop no longer serialises handlers
        probe.Peak.Should().BeLessThanOrEqualTo(4); // and never exceeds the configured limit
    }

    [Fact]
    public async Task Keeps_messages_sharing_a_partition_key_in_order()
    {
        var probe = new ConcurrencyProbe { Hold = TimeSpan.FromMilliseconds(30) };
        using var host = BuildHost(
            o =>
            {
                o.MaxConcurrentMessages = 4;
                o.PreserveKeyOrder = true;
            },
            probe);

        await host.StartAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        for (var i = 0; i < 6; i++)
            await bus.PublishAsync(new SlowEvent($"a{i}"), new PublishOptions { PartitionKey = "alpha" });

        await WaitForCompletionAsync(probe, 6, TimeSpan.FromSeconds(15));
        await host.StopAsync();

        // One key → one worker → publish order survives, even though 4 workers are running.
        probe.Started.Should().ContainInOrder("a0", "a1", "a2", "a3", "a4", "a5");
        probe.Completed.Should().ContainInOrder("a0", "a1", "a2", "a3", "a4", "a5");
    }

    [Fact]
    public async Task Drains_in_flight_messages_on_stop()
    {
        var probe = new ConcurrencyProbe { Hold = TimeSpan.FromMilliseconds(400) };
        using var host = BuildHost(o => o.MaxConcurrentMessages = 4, probe);
        await host.StartAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        for (var i = 0; i < 4; i++)
            await bus.PublishAsync(new SlowEvent($"m{i}"));

        // Stop while every handler is still holding its slot: the drain, not the wait, is what lets them finish.
        await WaitForStartAsync(probe, 4, TimeSpan.FromSeconds(10));
        probe.Completed.Should().BeEmpty();

        await host.StopAsync();

        probe.Completed.Should().HaveCount(4);
    }

    private static async Task WaitForStartAsync(ConcurrencyProbe probe, int count, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (probe.Started.Count < count && DateTime.UtcNow < deadline)
            await Task.Delay(10, CancellationToken.None);
    }

    private static async Task WaitForCompletionAsync(ConcurrencyProbe probe, int count, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (probe.Completed.Count < count && DateTime.UtcNow < deadline)
            await Task.Delay(10, CancellationToken.None);
    }
}
