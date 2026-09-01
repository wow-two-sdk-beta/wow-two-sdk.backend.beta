using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Maps an event type to its dispatcher. Built at registration time, resolved as a singleton.</summary>
internal sealed class EventDispatcherRegistry
{
    private readonly Dictionary<Type, EventDispatcher> _dispatchers = [];

    public void Register(Type eventType, EventDispatcher dispatcher) => _dispatchers[eventType] = dispatcher;

    public bool Contains(Type eventType) => _dispatchers.ContainsKey(eventType);

    public bool TryGet(Type eventType, [MaybeNullWhen(false)] out EventDispatcher dispatcher) => _dispatchers.TryGetValue(eventType, out dispatcher);
}
