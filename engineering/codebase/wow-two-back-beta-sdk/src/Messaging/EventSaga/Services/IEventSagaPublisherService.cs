using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga.Services;

/// <summary>Defines guarded publishing of a saga step event through the registered event bus.</summary>
public interface IEventSagaPublisherService
{
    /// <summary>Send an event to a destination, carrying the saga's correlation id.</summary>
    /// <typeparam name="TEvent">Event type.</typeparam>
    /// <param name="destination">
    /// Destination (queue) name. On a key-routed transport this must be a bound address, because an unbound key
    /// is dropped rather than refused. Declare it with <see cref="EventSagaBuilder.SendsTo{TEvent}"/> and registration
    /// binds it; the bound set is otherwise only the endpoint queue names and the consumed types' keys. The default
    /// transport checks the address against the local topology and applies
    /// <see cref="EventSagaOptions.UnroutableDestination"/> to one it cannot account for.
    /// </param>
    /// <param name="event">The event.</param>
    /// <param name="context">The saga context (for correlation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SendAsync<TEvent>(string destination, TEvent @event, EventSagaContext context, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}
