namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

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

    /// <summary>
    /// The transport ranks queued messages by a per-message priority, so a higher-priority message overtakes a
    /// lower-priority one already waiting (RabbitMQ priority queues — the AMQP <c>priority</c> property, honoured on a
    /// queue declared with <c>x-max-priority</c>). False where the concept does not exist on the wire (Kafka, NATS) and
    /// the only emulation is a queue per priority band.
    /// </summary>
    bool NativePriority { get; }

    /// <summary>
    /// The transport expires an individual message once its own deadline passes (RabbitMQ's per-message
    /// <c>expiration</c> property, in milliseconds). Distinct from a topic/stream-wide retention policy such as Kafka's
    /// <c>retention.ms</c> or JetStream's <c>MaxAge</c>, which ages every message out on one shared clock and cannot
    /// honour a per-message hint — a broker with only retention reports false here.
    /// </summary>
    bool NativeTimeToLive { get; }

    /// <summary>
    /// The transport can enqueue a message for an absolute point in time and hold it until then (Azure Service Bus
    /// <c>ScheduledEnqueueTimeUtc</c>). Distinct from <see cref="NativeDelay"/>, which is a relative offset applied at
    /// send time: a broker that only understands "in 30 seconds" cannot express "at 09:00 tomorrow" across a restart.
    /// </summary>
    bool NativeScheduling { get; }

    /// <summary>
    /// The transport binds a group of related messages to a single consumer for the life of the group — session /
    /// FIFO-group affinity (Azure Service Bus sessions, SQS FIFO message groups). Stronger than
    /// <see cref="NativeOrdering"/>: ordering alone says messages arrive in sequence, sessions add an exclusive lock so
    /// one consumer owns the group and can keep state across its messages.
    /// </summary>
    bool NativeSessions { get; }

    /// <summary>
    /// The broker acknowledges a publish, so the send path learns the message was durably accepted rather than merely
    /// written to a socket (RabbitMQ publisher confirms, Kafka's <c>acks</c> plus the returned offset, JetStream's
    /// <c>PubAck</c>). False means a successful send call proves nothing beyond local hand-off.
    /// </summary>
    bool NativePublisherConfirms { get; }

    /// <summary>
    /// The transport offers producer-side transactions — a batch of publishes commits atomically, and consume offsets
    /// can commit inside the same transaction for exactly-once processing (Kafka EOS via <c>transactional.id</c>).
    /// Atomic-batch-publish without idempotent-producer semantics (AMQP's <c>tx.select</c>) does not qualify.
    /// </summary>
    bool NativeTransactions { get; }

    /// <summary>
    /// The transport has a built-in reply-address mechanism, so a request carries where its response goes without the
    /// SDK provisioning a reply queue and correlating by hand (NATS request-reply over an <c>_INBOX</c> subject,
    /// RabbitMQ direct-reply-to over the <c>amq.rabbitmq.reply-to</c> pseudo-queue).
    /// </summary>
    bool NativeRequestReply { get; }

    /// <summary>
    /// Settlement runs through the <see cref="ReceiveContext"/> handle and is safe to call off the consume thread
    /// (RabbitMQ ack-by-delivery-tag, NATS ack-by-message, the in-memory no-op). False when settlement is a property of
    /// the consume loop itself — Kafka advances offsets on the loop, so a worker thread must not settle out of band.
    /// </summary>
    bool SettlesInContext { get; }

    /// <summary>
    /// The transport's consume/settle API is bound to the thread that owns the consumer and must not be driven from a
    /// worker (librdkafka's <c>Consume</c>/<c>StoreOffset</c>). The concurrency pump degrades to sequential dispatch for
    /// such a transport; parallelism there needs offsets marshalled back to the consume loop.
    /// </summary>
    bool ThreadAffineConsume { get; }
}
