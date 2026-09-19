using System.Collections.Concurrent;
using System.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// One message as the harness saw it at a single point on the pipeline — the envelope plus whatever that point knew
/// (the consume outcome, the exception). Which point it was is the log it landed in, not a field here.
/// </summary>
public sealed record RecordedMessage
{
    /// <summary>The envelope the observer was handed. On the consume side this is the reconstructed envelope, so <see cref="Body"/> is the deserialized event.</summary>
    public required EventEnvelopeModel Envelope { get; init; }

    /// <summary>How the delivery attempt ended. Set only on <see cref="MessagingTestHarness.Consumed"/>; <c>null</c> everywhere else.</summary>
    public ConsumeOutcome? Outcome { get; init; }

    /// <summary>The fault, on the fault logs (<see cref="MessagingTestHarness.Faulted"/>, <see cref="MessagingTestHarness.DeadLettered"/>, <see cref="MessagingTestHarness.PublishFaults"/>); <c>null</c> on the success logs.</summary>
    public Exception? Exception { get; init; }

    /// <summary>Wall-clock time the harness recorded this — for ordering across logs in a failure dump.</summary>
    public required DateTimeOffset RecordedAtUtc { get; init; }

    /// <summary>The transport message id.</summary>
    public string MessageId => Envelope.MessageId;

    /// <summary>The destination (queue/topic) the message was published to or received from.</summary>
    public string Destination => Envelope.Destination;

    /// <summary>The event payload.</summary>
    public object Body => Envelope.Body;

    /// <summary>Runtime type of <see cref="Body"/>.</summary>
    public Type EventType => Envelope.BodyType;

    /// <summary>True when the payload is a <typeparamref name="TEvent"/> (assignability, so a base type or interface matches too).</summary>
    /// <typeparam name="TEvent">The event contract to test for.</typeparam>
    public bool Is<TEvent>()
        where TEvent : class, IEvent
        => Envelope.Body is TEvent;

    /// <summary>The payload as a <typeparamref name="TEvent"/>.</summary>
    /// <typeparam name="TEvent">The event contract to cast to.</typeparam>
    /// <exception cref="InvalidCastException">The payload is not a <typeparamref name="TEvent"/>.</exception>
    public TEvent BodyAs<TEvent>()
        where TEvent : class, IEvent
        => Envelope.Body as TEvent ?? throw new InvalidCastException($"Message '{MessageId}' carries a {EventType.Name}, not a {typeof(TEvent).Name}.");
}
