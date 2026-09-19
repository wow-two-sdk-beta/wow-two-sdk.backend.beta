using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga.Services;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>Refers to what the default <see cref="IEventSagaPublisherService"/> does when a step addresses a destination this process does not bind.</summary>
public enum UnroutableDestinationBehavior
{
    /// <summary>
    /// Log once per destination and send anyway. The default, because an address consumed by <em>another</em> service
    /// is unbound here and perfectly legitimate — this process cannot see a remote binding.
    /// </summary>
    Warn = 0,

    /// <summary>
    /// Throw <see cref="InvalidOperationException"/> instead of sending. For a service whose saga destinations are all
    /// local: every one is then declarable via <see cref="EventSagaBuilder.SendsTo{TEvent}"/>, so anything unbound is a
    /// configuration error and a silent drop is never the right outcome.
    /// </summary>
    Throw = 1,
}
