using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Handles price requests through the responder-side helper.</summary>
public sealed class PriceRequestedHandler : IEventHandler<PriceRequested>
{
    public ValueTask HandleAsync(EventContext<PriceRequested> context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.RespondAsync(new PriceQuoted { OrderId = context.Event.OrderId, Amount = 42m }, cancellationToken);
    }
}
