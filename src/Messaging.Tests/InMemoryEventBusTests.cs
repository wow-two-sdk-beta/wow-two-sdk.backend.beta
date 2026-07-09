using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

public sealed class InMemoryEventBusTests
{
    private static async Task<IHost> StartHostAsync(Action<InMemoryEventBusOptions>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<EventCollector>();
        builder.Services.AddInMemoryEventBus(configure, typeof(PingHandler).Assembly);
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    [Fact]
    public async Task Publishes_and_handles_event()
    {
        using var host = await StartHostAsync();
        var collector = host.Services.GetRequiredService<EventCollector>();
        var bus = host.Services.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new PingEvent("hello"));

        (await collector.WaitForCountAsync(1, TimeSpan.FromSeconds(5))).Should().BeTrue();
        collector.Count.Should().Be(1);
        await host.StopAsync();
    }

    [Fact]
    public async Task Deduplicates_by_message_id()
    {
        using var host = await StartHostAsync();
        var collector = host.Services.GetRequiredService<EventCollector>();
        var bus = host.Services.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new PingEvent("a"), new PublishOptions { MessageId = "dup-1" });
        await bus.PublishAsync(new PingEvent("b"), new PublishOptions { MessageId = "dup-1" });

        (await collector.WaitForCountAsync(1, TimeSpan.FromSeconds(5))).Should().BeTrue();
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        collector.Count.Should().Be(1);
        await host.StopAsync();
    }

    [Fact]
    public async Task Dead_letters_after_retries_exhausted()
    {
        using var host = await StartHostAsync(o => o.Retry = new RetryConfig(MaxAttempts: 2, Backoff: BackoffKind.None));
        var bus = host.Services.GetRequiredService<IEventBus>();
        var deadLetters = host.Services.GetRequiredService<IDeadLetterStore>();

        await bus.PublishAsync(new BoomEvent("x"), new PublishOptions { MessageId = "boom-1" });

        var found = await WaitForDeadLetterAsync(deadLetters, source: nameof(BoomEvent), messageId: "boom-1", TimeSpan.FromSeconds(5));
        found.Should().BeTrue();
        await host.StopAsync();
    }

    private static async Task<bool> WaitForDeadLetterAsync(IDeadLetterStore store, string source, string messageId, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            await foreach (var record in store.ReadAsync(source, CancellationToken.None))
                if (record.MessageId == messageId)
                    return true;

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return false;
    }
}
