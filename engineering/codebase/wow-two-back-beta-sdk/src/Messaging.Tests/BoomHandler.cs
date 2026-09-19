using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Handles test events by always throwing.</summary>
public sealed class BoomHandler : IEventHandler<BoomEvent>
{
    public ValueTask HandleAsync(EventContext<BoomEvent> context, CancellationToken cancellationToken)
        => throw new InvalidOperationException("boom");
}
