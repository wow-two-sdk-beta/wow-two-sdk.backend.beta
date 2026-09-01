using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>
/// The messaging layer's metrics seam. The default implementation records to the <see cref="MessagingMeterConstants.Name"/>
/// <see cref="Meter"/>; replace the registration to route messaging telemetry elsewhere, or register
/// <see cref="NoOpMessagingMetrics"/> to switch it off entirely.
/// </summary>
/// <remarks>
///   - never throw — the publish and consume paths do not guard the call sites
///   - never tag with message id, correlation id, or partition key — each is unbounded and blows up cardinality
/// </remarks>
public interface IMessagingMetrics
{
    /// <summary>Record one message handed to the transport for delivery.</summary>
    /// <param name="destination">Logical destination (queue/topic) the message was sent to.</param>
    /// <param name="eventType">The event contract type.</param>
    void RecordPublished(string destination, Type eventType);

    /// <summary>Record the terminal outcome of one received message.</summary>
    /// <param name="destination">Logical destination the message arrived on.</param>
    /// <param name="eventType">The event contract type.</param>
    /// <param name="outcome">How the message left the pipeline.</param>
    void RecordConsumed(string destination, Type eventType, ConsumeOutcome outcome);

    /// <summary>Record how long one received message spent in the consume pipeline — retries and settlement included.</summary>
    /// <param name="destination">Logical destination the message arrived on.</param>
    /// <param name="eventType">The event contract type.</param>
    /// <param name="elapsed">Wall time spent processing the message.</param>
    void RecordConsumeDuration(string destination, Type eventType, TimeSpan elapsed);

    /// <summary>Record one message moved aside after processing was exhausted.</summary>
    /// <param name="destination">Logical destination the message arrived on.</param>
    /// <param name="eventType">The event contract type.</param>
    /// <param name="exception">The terminal exception. Only its <b>type</b> is recorded — the message text is unbounded and never tagged.</param>
    void RecordDeadLettered(string destination, Type eventType, Exception? exception);

    /// <summary>Record one redelivery attempt made by the in-process resilience pipeline (broker-side redelivery is invisible here).</summary>
    /// <param name="destination">Logical destination the message arrived on.</param>
    /// <param name="eventType">The event contract type.</param>
    void RecordRetried(string destination, Type eventType);

    /// <summary>Register a probe that the in-flight gauge reads on each collection cycle. Several probes sum into one measurement.</summary>
    /// <param name="probe">Returns the number of messages currently being processed.</param>
    /// <returns>A handle that unregisters the probe when disposed.</returns>
    IDisposable TrackInFlight(Func<int> probe);
}
