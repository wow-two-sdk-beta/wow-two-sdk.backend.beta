namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Poison-message terminus with replay (redrive). Native broker DLQs back this on adapters; in-memory by default.</summary>
/// <remarks>
///   - the floor — park a message, put it back
///   - browse-by-criteria, by-id lookup, purge and quarantine need <see cref="IDeadLetterQueryRepository"/>
///   - <see cref="IDeadLetterAdmin"/> falls back to this interface where a store offers no query repository
/// </remarks>
public interface IDeadLetterRepository
{
    /// <summary>Move a message into the dead-letter store.</summary>
    /// <param name="record">The dead-letter record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DeadLetterAsync(DeadLetterRecord record, CancellationToken cancellationToken);

    /// <summary>Enumerate dead-lettered messages for a source destination.</summary>
    /// <param name="source">The source destination/queue name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<DeadLetterRecord> ReadAsync(string source, CancellationToken cancellationToken);

    /// <summary>Replay (redrive) a dead-lettered message back to its source with a reset delivery count.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    ///   - replay the record as currently stored, never a copy cached at dead-letter time
    ///   - <see cref="IDeadLetterAdmin"/> stamps the redrive marker on the stored record before calling this
    ///   - the counter reaches the wire through the store's own replay, not a second publish path
    /// </remarks>
    ValueTask ReplayAsync(string messageId, CancellationToken cancellationToken);
}
