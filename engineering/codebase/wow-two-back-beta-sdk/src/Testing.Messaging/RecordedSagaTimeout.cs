using System.Collections.Concurrent;
using System.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>A saga timeout the harness saw go out on the wire, with the headers that decide whether the instance will still accept it.</summary>
/// <remarks>
///   - projected from <see cref="MessagingTestHarness.Published"/>
///   - a record proves the transport has already parked the timeout
///   - advance a fake clock only after the record appears, or the advance races the schedule
/// </remarks>
public sealed record RecordedSagaTimeout
{
    /// <summary>The timeout's declared name — the key its token lives under in <see cref="ISagaState.TimeoutTokens"/>, and what <c>Unschedule</c> cancels.</summary>
    public required string Name { get; init; }

    /// <summary>The token minted when it was scheduled. An arriving timeout whose token no longer matches the instance's is dropped as stale.</summary>
    public required string Token { get; init; }

    /// <summary>The instance the timeout is addressed to.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>When the transport may deliver it; <c>null</c> on a transport that carries no delivery time.</summary>
    public required DateTimeOffset? DueUtc { get; init; }

    /// <summary>The published envelope.</summary>
    public required EventEnvelope Envelope { get; init; }

    /// <summary>The transport message id — the same id the delivery is recorded under, because the scheduler parks and re-enqueues the one envelope.</summary>
    public string MessageId => Envelope.MessageId;

    /// <summary>The timeout event payload.</summary>
    public object Event => Envelope.Body;

    /// <summary>Runtime type of <see cref="Event"/>.</summary>
    public Type EventType => Envelope.BodyType;

    /// <summary>The timeout as <c>name@due</c>.</summary>
    public override string ToString() => $"{Name}@{DueUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "immediate"}";
}
