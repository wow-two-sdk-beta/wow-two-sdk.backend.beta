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
        var context = new EventContext<TEvent>(@event, envelope, bus);
        foreach (var handler in services.GetServices<IEventHandler<TEvent>>())
            await handler.HandleAsync(context, cancellationToken);
    }
}

/// <summary>Maps an event type to its dispatcher. Built at registration time, resolved as a singleton.</summary>
internal sealed class EventDispatcherRegistry
{
    private readonly Dictionary<Type, EventDispatcher> _dispatchers = [];

    public void Register(Type eventType, EventDispatcher dispatcher) => _dispatchers[eventType] = dispatcher;

    public bool Contains(Type eventType) => _dispatchers.ContainsKey(eventType);

    public bool TryGet(Type eventType, [MaybeNullWhen(false)] out EventDispatcher dispatcher) => _dispatchers.TryGetValue(eventType, out dispatcher);
}
