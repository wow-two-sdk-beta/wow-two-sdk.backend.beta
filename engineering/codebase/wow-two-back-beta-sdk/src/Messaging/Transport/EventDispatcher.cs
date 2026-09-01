using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Type-erased dispatcher base — resolves and invokes the handlers for one event type. Transport-agnostic.</summary>
internal abstract class EventDispatcher
{
    public abstract ValueTask DispatchAsync(IServiceProvider services, EventEnvelope envelope, IEventBus bus, CancellationToken cancellationToken);
}

/// <summary>Strongly-typed dispatcher for <typeparamref name="TEvent"/>.</summary>
internal sealed class EventDispatcher<TEvent> : EventDispatcher
    where TEvent : class, IEvent
{
    public override async ValueTask DispatchAsync(IServiceProvider services, EventEnvelope envelope, IEventBus bus, CancellationToken cancellationToken)
    {
        var @event = (TEvent)envelope.Body;
        var context = new EventContext<TEvent>(@event, envelope, bus, services.GetService<IMessageHeaderPropagationPolicy>());
        foreach (var handler in services.GetServices<IEventHandler<TEvent>>())
            await handler.HandleAsync(context, cancellationToken);
    }
}
