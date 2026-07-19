using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>How a received message left the consume pipeline — the <c>messaging.consume.outcome</c> tag on the consumed counter.</summary>
public enum ConsumeOutcome
{
    /// <summary>The message reached its handler and processing completed.</summary>
    Success,

    /// <summary>Processing was exhausted (retries included); the message was dead-lettered.</summary>
    Faulted,

    /// <summary>The inbox had already processed this message id, so dispatch was skipped.</summary>
    Duplicate,

    /// <summary>No handler is registered for the event type; the message was settled without dispatch.</summary>
    NoHandler,
}

/// <summary>
/// Names of the messaging meter and its instruments — the metrics counterpart to the messaging <c>ActivitySource</c>
/// in <c>MessagingDiagnostics</c>. Collection is automatic under <c>AddOpenTelemetryMetrics</c> (it adds the
/// <c>WoW.Two.*</c> meter prefix); a hand-rolled MeterProvider adds <see cref="Name"/> explicitly.
/// </summary>
/// <remarks>
/// Instrument names follow the OpenTelemetry messaging semantic conventions where one exists
/// (<see cref="SentMessages"/>, <see cref="ConsumedMessages"/>, <see cref="ProcessDuration"/>); the dead-letter,
/// retry, and in-flight instruments have no convention yet and sit in the same namespace.
/// </remarks>
public static class MessagingMeter
{
    /// <summary>The messaging meter name.</summary>
    public const string Name = "WoW.Two.Sdk.Messaging";

    /// <summary>Counter — messages handed to the transport for delivery.</summary>
    public const string SentMessages = "messaging.client.sent.messages";

    /// <summary>Counter — received messages that left the consume pipeline, tagged with their <see cref="ConsumeOutcome"/>.</summary>
    public const string ConsumedMessages = "messaging.client.consumed.messages";

    /// <summary>Histogram (seconds) — time one received message spent in the consume pipeline.</summary>
    public const string ProcessDuration = "messaging.process.duration";

    /// <summary>Counter — messages moved aside after processing was exhausted.</summary>
    public const string DeadLetteredMessages = "messaging.process.dead_lettered.messages";

    /// <summary>Counter — redelivery attempts made by the in-process resilience pipeline.</summary>
    public const string RetriedMessages = "messaging.process.retried.messages";

    /// <summary>Observable gauge — messages currently queued to or executing on a consume worker.</summary>
    public const string InFlightMessages = "messaging.process.in_flight.messages";
}

/// <summary>
/// The messaging layer's metrics seam. The default implementation records to the <see cref="MessagingMeter.Name"/>
/// <see cref="Meter"/>; replace the registration to route messaging telemetry elsewhere, or register
/// <see cref="NoOpMessagingMetrics"/> to switch it off entirely.
/// </summary>
/// <remarks>
/// Implementations MUST NOT throw: every member sits on the publish or consume path, which treats metrics as pure
/// observation and does not guard the call sites. Implementations MUST NOT tag with message id, correlation id, or
/// partition key — each is unbounded and would blow up cardinality.
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

/// <summary>
/// <see cref="Meter"/>-backed <see cref="IMessagingMetrics"/> on <see cref="MessagingMeter.Name"/>.
/// </summary>
/// <remarks>
/// The <see cref="Meter"/> is per-instance rather than static (unlike the ActivitySource in <c>MessagingDiagnostics</c>)
/// because the in-flight gauge holds callbacks into a container's <c>MessagePump</c>. A static meter would accumulate
/// probes from every container ever built in the process — a real leak under a test suite that spins up many hosts.
/// Container disposal disposes the meter and with it the gauge.
/// </remarks>
internal sealed class DefaultMessagingMetrics : IMessagingMetrics, IDisposable
{
    private const string DestinationTag = "messaging.destination.name";
    private const string MessageTypeTag = "messaging.message.type";
    private const string OutcomeTag = "messaging.consume.outcome";
    private const string ErrorTypeTag = "error.type";

    private readonly Meter _meter = new(MessagingMeter.Name);
    private readonly Counter<long> _sent;
    private readonly Counter<long> _consumed;
    private readonly Histogram<double> _processDuration;
    private readonly Counter<long> _deadLettered;
    private readonly Counter<long> _retried;
    private readonly Lock _probeLock = new();
    private readonly List<Func<int>> _inFlightProbes = [];

    public DefaultMessagingMetrics()
    {
        _sent = _meter.CreateCounter<long>(
            MessagingMeter.SentMessages,
            unit: "{message}",
            description: "Messages handed to the transport for delivery.");

        _consumed = _meter.CreateCounter<long>(
            MessagingMeter.ConsumedMessages,
            unit: "{message}",
            description: "Received messages that left the consume pipeline, by outcome.");

        _processDuration = _meter.CreateHistogram<double>(
            MessagingMeter.ProcessDuration,
            unit: "s",
            description: "Time one received message spent in the consume pipeline, retries and settlement included.");

        _deadLettered = _meter.CreateCounter<long>(
            MessagingMeter.DeadLetteredMessages,
            unit: "{message}",
            description: "Messages moved aside after processing was exhausted.");

        _retried = _meter.CreateCounter<long>(
            MessagingMeter.RetriedMessages,
            unit: "{message}",
            description: "Redelivery attempts made by the in-process resilience pipeline.");

        // The Meter owns the instrument — no field needed to keep the gauge alive. T is explicit: without it the
        // Func<T> overload is a candidate too and inference goes ambiguous.
        _meter.CreateObservableGauge<int>(
            MessagingMeter.InFlightMessages,
            ObserveInFlight,
            unit: "{message}",
            description: "Messages currently queued to or executing on a consume worker.");
    }

    public void RecordPublished(string destination, Type eventType)
        => _sent.Add(1, BuildTags(destination, eventType));

    public void RecordConsumed(string destination, Type eventType, ConsumeOutcome outcome)
    {
        var tags = BuildTags(destination, eventType);
        tags.Add(OutcomeTag, OutcomeName(outcome));
        _consumed.Add(1, tags);
    }

    public void RecordConsumeDuration(string destination, Type eventType, TimeSpan elapsed)
        => _processDuration.Record(elapsed.TotalSeconds, BuildTags(destination, eventType));

    public void RecordDeadLettered(string destination, Type eventType, Exception? exception)
    {
        var tags = BuildTags(destination, eventType);
        tags.Add(ErrorTypeTag, TypeName(exception?.GetType())); // type only — an exception message is unbounded
        _deadLettered.Add(1, tags);
    }

    public void RecordRetried(string destination, Type eventType)
        => _retried.Add(1, BuildTags(destination, eventType));

    public IDisposable TrackInFlight(Func<int> probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        lock (_probeLock)
        {
            _inFlightProbes.Add(probe);
        }

        return new ProbeRegistration(this, probe);
    }

    public void Dispose() => _meter.Dispose();

    private static TagList BuildTags(string destination, Type eventType) => new()
    {
        { DestinationTag, destination },
        { MessageTypeTag, TypeName(eventType) },
    };

    private static string TypeName(Type? type) => type is null ? "unknown" : type.FullName ?? type.Name;

    private static string OutcomeName(ConsumeOutcome outcome) => outcome switch
    {
        ConsumeOutcome.Success => "success",
        ConsumeOutcome.Faulted => "faulted",
        ConsumeOutcome.Duplicate => "duplicate",
        ConsumeOutcome.NoHandler => "no_handler",
        _ => "unknown",
    };

    private IEnumerable<Measurement<int>> ObserveInFlight()
    {
        lock (_probeLock)
        {
            if (_inFlightProbes.Count == 0)
                return [];

            var total = 0;
            foreach (var probe in _inFlightProbes)
            {
                try
                {
                    total += probe();
                }
                catch (Exception)
                {
                    // A faulted probe must not take down collection for the whole meter.
                }
            }

            return [new Measurement<int>(total)];
        }
    }

    private sealed class ProbeRegistration(DefaultMessagingMetrics owner, Func<int> probe) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._probeLock)
            {
                owner._inFlightProbes.Remove(probe);
            }
        }
    }
}

/// <summary>
/// <see cref="IMessagingMetrics"/> that records nothing — so the messaging layer never forces a metrics dependency.
/// Register it before the transport (<c>services.AddSingleton&lt;IMessagingMetrics&gt;(NoOpMessagingMetrics.Instance)</c>)
/// and the default registration, which is <c>TryAdd</c>-based, stands down.
/// </summary>
public sealed class NoOpMessagingMetrics : IMessagingMetrics
{
    /// <summary>The shared instance — the type is stateless.</summary>
    public static readonly NoOpMessagingMetrics Instance = new();

    void IMessagingMetrics.RecordPublished(string destination, Type eventType) { }

    void IMessagingMetrics.RecordConsumed(string destination, Type eventType, ConsumeOutcome outcome) { }

    void IMessagingMetrics.RecordConsumeDuration(string destination, Type eventType, TimeSpan elapsed) { }

    void IMessagingMetrics.RecordDeadLettered(string destination, Type eventType, Exception? exception) { }

    void IMessagingMetrics.RecordRetried(string destination, Type eventType) { }

    IDisposable IMessagingMetrics.TrackInFlight(Func<int> probe) => NullRegistration.Instance;

    private sealed class NullRegistration : IDisposable
    {
        public static readonly NullRegistration Instance = new();

        public void Dispose() { }
    }
}

/// <summary>Provides registration for the messaging metrics seam.</summary>
public static class MessagingMetricsServiceCollectionExtensions
{
    /// <summary>
    /// Register the default <see cref="Meter"/>-backed <see cref="IMessagingMetrics"/>. <c>TryAdd</c>-based, so a
    /// consumer registering their own implementation (or <see cref="NoOpMessagingMetrics"/>) first keeps it.
    /// Every transport registration path calls this; calling it directly is only needed for a hand-rolled composition.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddMessagingMetrics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMessagingMetrics, DefaultMessagingMetrics>();
        return services;
    }
}
