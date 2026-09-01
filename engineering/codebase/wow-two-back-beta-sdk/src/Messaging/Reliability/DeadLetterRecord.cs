namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>A message that exhausted retries (or was rejected as non-retryable) and was moved aside.</summary>
/// <param name="MessageId">The dead-lettered message id.</param>
/// <param name="Destination">The source destination/queue.</param>
/// <param name="Reason">Human-readable failure reason.</param>
/// <param name="ExceptionType">Full name of the terminal exception type, if any.</param>
/// <param name="Envelope">The original envelope, retained for replay.</param>
/// <param name="DeadLetteredAtUtc">When the message was dead-lettered.</param>
public sealed record DeadLetterRecord(
    string MessageId,
    string Destination,
    string Reason,
    string? ExceptionType,
    EventEnvelope Envelope,
    DateTimeOffset DeadLetteredAtUtc)
{
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
    public static DeadLetterRecord From(EventEnvelope envelope, Exception exception, DateTimeOffset deadLetteredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(exception);
        return new DeadLetterRecord(envelope.MessageId, envelope.Destination, exception.Message, exception.GetType().FullName, envelope, deadLetteredAtUtc)
        {
            // Recover the redrive marker from the envelope so an Nth-replay death is stored with its count.
            RedriveCount = DeadLetterHeaderConstants.ReadRedriveCount(envelope),
        };
    }
}
