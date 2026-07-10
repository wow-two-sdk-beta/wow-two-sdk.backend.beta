using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>The consume-filter pipeline: registered filters wrap the dispatch core, ordered by registration.</summary>
public sealed class ConsumeFilterTests
{
    private static readonly ConcurrentQueue<string> Trace = new();

    private sealed class FilterA : IConsumeFilter
    {
        public async ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
        {
            Trace.Enqueue("A:before");
            await next(context, cancellationToken);
            Trace.Enqueue("A:after");
        }
    }

    private sealed class FilterB : IConsumeFilter
    {
        public async ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
        {
            Trace.Enqueue("B:before");
            await next(context, cancellationToken);
            Trace.Enqueue("B:after");
        }
    }

    [Fact]
    public async Task Filters_wrap_the_handler_in_registration_order()
    {
        Trace.Clear();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<EventCollector>();
        builder.Services.AddInMemoryEventBus(typeof(PingHandler).Assembly);
        builder.Services.AddConsumeFilter<FilterA>();
        builder.Services.AddConsumeFilter<FilterB>();
        using var host = builder.Build();
        await host.StartAsync();

        var collector = host.Services.GetRequiredService<EventCollector>();
        var bus = host.Services.GetRequiredService<IEventBus>();
        await bus.PublishAsync(new PingEvent("x"));

        (await collector.WaitForCountAsync(1, TimeSpan.FromSeconds(5))).Should().BeTrue(); // handler ran through the chain
        await host.StopAsync();

        // A registered first = outermost; the handler runs innermost between the before/after pairs.
        Trace.Should().ContainInOrder("A:before", "B:before", "B:after", "A:after");
    }
}
