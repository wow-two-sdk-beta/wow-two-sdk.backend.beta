using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>Holds options for declarative (routing-slip) event sagas.</summary>
public sealed record EventSagaOptions
{
    /// <summary>
    /// How a send to an unbound destination is handled. Default <see cref="UnroutableDestinationBehavior.Warn"/>.
    /// A destination declared through <see cref="EventSagaBuilder.SendsTo{TEvent}"/> whose type this process does not
    /// consume is exempt either way — it is a known cross-service address, not an accident.
    /// </summary>
    public UnroutableDestinationBehavior UnroutableDestination { get; set; } = UnroutableDestinationBehavior.Warn;
}
