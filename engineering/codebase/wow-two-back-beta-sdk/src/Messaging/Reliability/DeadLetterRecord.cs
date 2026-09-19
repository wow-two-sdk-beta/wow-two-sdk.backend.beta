using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>A message that exhausted retries (or was rejected as non-retryable) and was moved aside.</summary>
public sealed record DeadLetterRecord
{
    /// <summary>The dead-lettered message id.</summary>
    public required string MessageId { get; init; }

    /// <summary>The source destination/queue.</summary>
    public required string Destination { get; init; }

    /// <summary>Human-readable failure reason.</summary>
    public required string Reason { get; init; }

    /// <summary>Full name of the terminal exception type, if any.</summary>
    public required string? ExceptionType { get; init; }

    /// <summary>The original envelope, retained for replay.</summary>
    public required EventEnvelopeModel Envelope { get; init; }

    /// <summary>When the message was dead-lettered.</summary>
    public required DateTimeOffset DeadLetteredAtUtc { get; init; }

    /// <summary>
    /// How many times an operator has already redriven this message; 0 for one that has never been replayed. Stamped by
    /// <see cref="IDeadLetterAdmin"/> on each redrive, and the number the redrive cap is measured against.
    /// </summary>
    /// <remarks>
    ///   - the store's copy, not the marker the envelope carries
    ///   - read <see cref="EffectiveRedriveCount"/> instead
    /// </remarks>
    public int RedriveCount { get; init; }

    /// <summary>When the message was last redriven, or <c>null</c> if it never has been.</summary>
    public DateTimeOffset? LastRedrivenAtUtc { get; init; }

    /// <summary>Administration state. <see cref="DeadLetterState.Quarantined"/> holds the record back from redrive.</summary>
    public DeadLetterState State { get; init; } = DeadLetterState.DeadLettered;

    /// <summary>
    /// Redrive count including the marker carried on the envelope itself — the number the infinite-redrive guard uses.
    /// </summary>
    /// <remarks>
    ///   - a message that dies again arrives as a fresh record with <see cref="RedriveCount"/> at 0
    ///   - only the <c>wt-dl-redrive-count</c> header survives the round trip
    ///   - the cap holds across replays, across a restart, and across an envelope-only store
    /// </remarks>
    public int EffectiveRedriveCount => Math.Max(RedriveCount, DeadLetterHeaderConstants.ReadRedriveCount(Envelope));

    /// <summary>Build a record from a failed delivery.</summary>
    /// <param name="envelope">The envelope being dead-lettered.</param>
    /// <param name="exception">The terminal exception.</param>
    /// <param name="deadLetteredAtUtc">The current UTC time.</param>
    public static DeadLetterRecord From(EventEnvelopeModel envelope, Exception exception, DateTimeOffset deadLetteredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(exception);
        return new DeadLetterRecord
        {
            MessageId = envelope.MessageId,
            Destination = envelope.Destination,
            Reason = exception.Message,
            ExceptionType = exception.GetType().FullName,
            Envelope = envelope,
            DeadLetteredAtUtc = deadLetteredAtUtc,
            // Recover the redrive marker from the envelope so an Nth-replay death is stored with its count.
            RedriveCount = DeadLetterHeaderConstants.ReadRedriveCount(envelope),
        };
    }
}
