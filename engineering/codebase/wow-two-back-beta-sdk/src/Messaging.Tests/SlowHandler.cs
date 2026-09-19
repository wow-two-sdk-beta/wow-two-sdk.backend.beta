using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Handles <see cref="SlowEvent"/> instances through the probe.</summary>
public sealed class SlowHandler(ConcurrencyProbe probe) : IEventHandler<SlowEvent>
{
    public async ValueTask HandleAsync(EventContext<SlowEvent> context, CancellationToken cancellationToken)
        => await probe.RunAsync(context.Event.Tag);
}
