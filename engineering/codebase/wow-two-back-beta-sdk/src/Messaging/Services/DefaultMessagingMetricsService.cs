using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Services;

/// <summary>
/// Provides messaging telemetry recording through the <see cref="MessagingMeterConstants.Name"/> <see cref="Meter"/>.
/// </summary>
/// <remarks>Disposing the container disposes the meter and, with it, the in-flight gauge.</remarks>
internal sealed class DefaultMessagingMetricsService : IMessagingMetricsService, IDisposable
{
    private const string DestinationTag = "messaging.destination.name";
    private const string MessageTypeTag = "messaging.message.type";
    private const string OutcomeTag = "messaging.consume.outcome";
    private const string ErrorTypeTag = "error.type";

    // Per-container meter: the gauge holds probes into this container's MessagePump and retires with it.
    private readonly Meter _meter = new(MessagingMeterConstants.Name);
    private readonly Counter<long> _sent;
    private readonly Counter<long> _consumed;
    private readonly Histogram<double> _processDuration;
    private readonly Counter<long> _deadLettered;
    private readonly Counter<long> _retried;
    private readonly Lock _probeLock = new();
    private readonly List<Func<int>> _inFlightProbes = [];

    public DefaultMessagingMetricsService()
    {
        _sent = _meter.CreateCounter<long>(
            MessagingMeterConstants.SentMessages,
            unit: "{message}",
            description: "Messages handed to the transport for delivery.");

        _consumed = _meter.CreateCounter<long>(
            MessagingMeterConstants.ConsumedMessages,
            unit: "{message}",
            description: "Received messages that left the consume pipeline, by outcome.");

        _processDuration = _meter.CreateHistogram<double>(
            MessagingMeterConstants.ProcessDuration,
            unit: "s",
            description: "Time one received message spent in the consume pipeline, retries and settlement included.");

        _deadLettered = _meter.CreateCounter<long>(
            MessagingMeterConstants.DeadLetteredMessages,
            unit: "{message}",
            description: "Messages moved aside after processing was exhausted.");

        _retried = _meter.CreateCounter<long>(
            MessagingMeterConstants.RetriedMessages,
            unit: "{message}",
            description: "Redelivery attempts made by the in-process resilience pipeline.");

        // The meter owns the gauge — it observes until the meter is disposed.
        _meter.CreateObservableGauge<int>(
            MessagingMeterConstants.InFlightMessages,
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
        ConsumeOutcome.Ignored => "ignored",
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

    private sealed class ProbeRegistration(DefaultMessagingMetricsService owner, Func<int> probe) : IDisposable
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
