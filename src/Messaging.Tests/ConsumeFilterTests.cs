using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

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
        await using var harness = await MessagingTestHarness.StartAsync(
            static services =>
            {
                services.AddSingleton<EventCollector>();
                services.AddConsumeFilter<FilterA>();
                services.AddConsumeFilter<FilterB>();
            },
            handlerAssemblies: [typeof(PingHandler).Assembly]);

        await harness.Bus.PublishAsync(new PingEvent("x"));

        // Each filter records its "after" leg as the chain unwinds, which is past the consume hook — so wait for the
        // whole message to land rather than only for the handler to have run.
        await harness.WaitForIdleAsync();

        harness.Consumed.Count<PingEvent>().Should().Be(1); // handler ran through the chain

        // A registered first = outermost; the handler runs innermost between the before/after pairs.
        Trace.Should().ContainInOrder("A:before", "B:before", "B:after", "A:after");
    }
}
