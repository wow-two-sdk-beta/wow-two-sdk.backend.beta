namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// The send half of a transport — publishes an <see cref="EventEnvelope"/> to the underlying medium (in-memory channel,
/// RabbitMQ exchange, Kafka topic, …). <see cref="IEventBus"/> sits over this: it builds the envelope, the transport
/// owns the wire format. A CAP adapter implements this cleanly; the in-memory transport just writes its channel.
/// </summary>
public interface ISendTransport
{
    /// <summary>Publish an envelope. The transport serializes <see cref="EventEnvelope.Body"/> and maps headers/routing as it sees fit.</summary>
    /// <param name="envelope">The envelope to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// A received message and its settlement handle. The processing pipeline runs dedupe → resilience → dispatch, then
/// <see cref="AcknowledgeAsync"/> on success or <see cref="DeadLetterAsync"/> on exhaustion. Each transport supplies
/// its own settlement (RabbitMQ ack/DLX, ASB complete/dead-letter, in-memory no-op / <c>IDeadLetterStore</c>).
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

/// <summary>
/// The receive half of a transport — subscribes and drives each received message into the processing pipeline.
/// Started/stopped by a hosted service; the callback is the transport-agnostic <c>EventProcessingPipeline</c>.
/// </summary>
public interface IReceiveTransport
{
    /// <summary>Begin receiving; invoke <paramref name="onMessage"/> for each message until stopped.</summary>
    /// <param name="onMessage">The processing callback (dedupe → resilience → dispatch → settle).</param>
    /// <param name="cancellationToken">Cancellation token tied to host lifetime.</param>
    ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken);

    /// <summary>Stop receiving and release transport resources.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Per-transport capability flags — the dispatch pipeline reads these to decide native-vs-emulated for each concern
/// (e.g. Kafka has no native DLQ → the SDK emulates it; RabbitMQ has native DLX → the adapter uses it).
/// </summary>
public interface ITransportCapabilities
{
    /// <summary>The transport has a native dead-letter facility (RabbitMQ DLX, ASB DLQ, SQS redrive).</summary>
    bool NativeDeadLetter { get; }

    /// <summary>The transport can natively delay / schedule delivery.</summary>
    bool NativeDelay { get; }

    /// <summary>The transport natively deduplicates by message id.</summary>
    bool NativeDedupe { get; }

    /// <summary>The transport provides an ordering guarantee (per partition / session / subject).</summary>
    bool NativeOrdering { get; }
}
