using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

/// <summary>Defines application-event publishing to subscribers and sending to a named destination through a transport.</summary>
/// <remarks>Completion reflects the underlying send transport operation; it does not by itself establish durability or completed handler execution.</remarks>
public interface IEventBus
{
    /// <summary>Publish an event — fan-out to every handler of <typeparamref name="TEvent"/>.</summary>
    /// <typeparam name="TEvent">Event type.</typeparam>
    /// <param name="event">The event payload.</param>
    /// <param name="options">Optional publish options (ids, delay, transport hints, headers).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PublishAsync<TEvent>(TEvent @event, PublishOptions? options = null, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>Send an event to a named destination — point-to-point (delivered to that destination's consumer, not fanned out).</summary>
    /// <typeparam name="TEvent">Event type.</typeparam>
    /// <param name="destination">Logical destination (queue) name.</param>
    /// <param name="event">The event payload.</param>
    /// <param name="options">Optional send options (ids, delay, partition key, transport hints, headers).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SendAsync<TEvent>(string destination, TEvent @event, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}
