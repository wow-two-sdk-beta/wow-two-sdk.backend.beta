using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Succeeds and records the event.</summary>
public sealed class PingHandler(EventCollector collector) : IEventHandler<PingEvent>
{
    public ValueTask HandleAsync(EventContext<PingEvent> context, CancellationToken cancellationToken)
    {
        collector.Record(context.Event);
        return ValueTask.CompletedTask;
    }
}
