using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>Handles an event of type <typeparamref name="TEvent"/>. Many handlers may handle the same event (fan-out).</summary>
/// <typeparam name="TEvent">Event contract type.</typeparam>
public interface IEventHandler<TEvent>
    where TEvent : class, IEvent
{
    /// <summary>Handle the event. Throw to trigger retry / dead-lettering per the configured policy.</summary>
    /// <param name="context">The event and its envelope, plus correlation-aware publish/send helpers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask HandleAsync(EventContext<TEvent> context, CancellationToken cancellationToken);
}
