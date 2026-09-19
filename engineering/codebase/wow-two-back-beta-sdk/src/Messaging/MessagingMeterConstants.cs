using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>
/// Holds names of the messaging meter and its instruments — the metrics counterpart to the messaging <c>ActivitySource</c>
/// in <c>MessagingDiagnosticConstants</c>. Collection is automatic under <c>AddOpenTelemetryMetrics</c> (it adds the
/// <c>WoW.Two.*</c> meter prefix); a hand-rolled MeterProvider adds <see cref="Name"/> explicitly.
/// </summary>
/// <remarks>
///   - <see cref="SentMessages"/>, <see cref="ConsumedMessages"/>, <see cref="ProcessDuration"/> follow the OpenTelemetry messaging conventions
///   - dead-letter, retry, and in-flight have no convention yet
///   - sit in the same namespace regardless
/// </remarks>
public static class MessagingMeterConstants
{
    /// <summary>Holds the messaging meter name.</summary>
    public const string Name = "WoW.Two.Sdk.Messaging";

    /// <summary>Holds counter — messages handed to the transport for delivery.</summary>
    public const string SentMessages = "messaging.client.sent.messages";

    /// <summary>Holds counter — received messages that left the consume pipeline, tagged with their <see cref="ConsumeOutcome"/>.</summary>
    public const string ConsumedMessages = "messaging.client.consumed.messages";

    /// <summary>Holds histogram (seconds) — time one received message spent in the consume pipeline.</summary>
    public const string ProcessDuration = "messaging.process.duration";

    /// <summary>Holds counter — messages moved aside after processing was exhausted.</summary>
    public const string DeadLetteredMessages = "messaging.process.dead_lettered.messages";

    /// <summary>Holds counter — redelivery attempts made by the in-process resilience pipeline.</summary>
    public const string RetriedMessages = "messaging.process.retried.messages";

    /// <summary>Holds observable gauge — messages currently queued to or executing on a consume worker.</summary>
    public const string InFlightMessages = "messaging.process.in_flight.messages";
}
