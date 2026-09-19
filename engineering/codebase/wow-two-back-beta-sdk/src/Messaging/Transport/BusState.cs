using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Refers to runtime state of the consume side of the bus — what <see cref="IBusControl.State"/> reports to an ops endpoint or a
/// health check.
/// </summary>
public enum BusState
{
    /// <summary>
    /// Not consuming. Either the host has not started the transport yet, or <see cref="IBusControl.StopAsync"/> has run
    /// and the consume workers were retired. Terminal once stopped — the bus does not restart in place.
    /// </summary>
    Stopped,

    /// <summary>Consuming normally — every received message flows into the processing pipeline.</summary>
    Running,

    /// <summary>
    /// Connected but not admitting. The transport stays subscribed while received messages park at the pipeline
    /// entrance, so nothing is dropped or buffered in-process and unconsumed messages stay unsettled at the broker,
    /// where prefetch throttles delivery. Work already in flight when the pause was requested runs to completion.
    /// </summary>
    Paused,

    /// <summary>Transient — inside <see cref="IBusControl.StopAsync"/>, waiting for in-flight messages to land before the bus reports <see cref="Stopped"/>.</summary>
    Draining,
}
