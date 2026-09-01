namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// A received message and its settlement handle. The processing pipeline runs dedupe → resilience → dispatch, then
/// <see cref="AcknowledgeAsync"/> on success or <see cref="DeadLetterAsync"/> on exhaustion. Each transport supplies
/// its own settlement (RabbitMQ ack/DLX, ASB complete/dead-letter, in-memory no-op / <c>IDeadLetterRepository</c>).
/// </summary>
public abstract class ReceiveContext
{
    /// <summary>The reconstructed envelope (the transport resolved the type + deserialized the body).</summary>
    public abstract EventEnvelope Envelope { get; }

    /// <summary>Acknowledge successful processing (settle the message).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public abstract ValueTask AcknowledgeAsync(CancellationToken cancellationToken);

    /// <summary>Move the message aside after retries are exhausted or it is non-retryable (native DLQ where available, else the SDK dead-letter store).</summary>
    /// <param name="reason">Why the message is being dead-lettered.</param>
    /// <param name="exception">The terminal exception, if any — carried into the dead-letter record and stamped as death headers on brokers that re-produce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public abstract ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken);
}
