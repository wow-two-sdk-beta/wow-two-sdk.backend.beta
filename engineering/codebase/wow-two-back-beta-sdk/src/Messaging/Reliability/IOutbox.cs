namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Transactional outbox port — enrolls an outgoing message in the ambient DB transaction (EF / CAP adapters implement this).</summary>
public interface IOutbox
{
    /// <summary>Stage a message for reliable publish within the current transaction.</summary>
    /// <param name="record">The outbox record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask EnqueueAsync(OutboxRecord record, CancellationToken cancellationToken);
}
