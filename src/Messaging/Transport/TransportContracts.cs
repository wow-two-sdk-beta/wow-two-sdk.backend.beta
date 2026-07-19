namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Wire-header conventions shared by every broker adapter — the reserved control namespace, the SDK's own control
/// header names, and the standard context headers the SDK understands. Adapters name headers from here rather than
/// from inline literals, so one constant governs the wire contract on every broker.
/// </summary>
public static class MessageHeaders
{
    /// <summary>
    /// Prefix reserved for SDK control headers — type token, content type, partition key, dead-letter death info.
    /// A caller-supplied header using this prefix is dropped on send so it cannot forge the wire contract.
    /// </summary>
    public const string ReservedPrefix = "wt-";

    /// <summary>Reserved. Stable type token of the body, so the consumer can resolve the CLR type.</summary>
    public const string EventType = ReservedPrefix + "event-type";

    /// <summary>Reserved. Content type of the serialized body, so the consumer selects the matching deserializer.</summary>
    public const string ContentType = ReservedPrefix + "content-type";

    /// <summary>Reserved. The envelope's <see cref="EventEnvelope.MessageId"/>, for brokers with no native message-id property.</summary>
    public const string MessageId = ReservedPrefix + "message-id";

    /// <summary>Reserved. The envelope's <see cref="EventEnvelope.PartitionKey"/>, so the consumer can preserve per-key ordering.</summary>
    public const string PartitionKey = ReservedPrefix + "partition-key";

    /// <summary>
    /// Reserved. The envelope's <see cref="EventEnvelope.ReplyTo"/> address, for brokers with no native reply-address
    /// property. RabbitMQ maps the native AMQP <c>reply-to</c> property instead and reads this only as a fallback.
    /// </summary>
    public const string ReplyTo = ReservedPrefix + "reply-to";

    /// <summary>
    /// Reserved. The envelope's <see cref="EventEnvelope.CorrelationId"/>, linking every message in one business flow.
    /// Distinct from <see cref="ConversationId"/>, which pairs a single reply with its request.
    /// </summary>
    public const string CorrelationId = ReservedPrefix + "correlation-id";

    /// <summary>Reserved. The envelope's <see cref="EventEnvelope.ConversationId"/>, pairing a reply with its request.</summary>
    public const string ConversationId = ReservedPrefix + "conversation-id";

    /// <summary>
    /// Reserved. The envelope's <see cref="EventEnvelope.DeliveryCount"/>, for brokers with no per-message redelivery
    /// counter of their own — a Kafka record is immutable and its offset says nothing about how often it was handed to
    /// a consumer, so the count has to travel with the message and be bumped by whoever re-produces it.
    /// </summary>
    public const string DeliveryCount = ReservedPrefix + "delivery-count";

    /// <summary>Reserved. Why a message was dead-lettered, stamped by adapters that re-produce onto a DLQ.</summary>
    public const string DeadLetterReason = ReservedPrefix + "dl-reason";

    /// <summary>Reserved. Type name of the terminal exception, stamped alongside <see cref="DeadLetterReason"/>.</summary>
    public const string DeadLetterExceptionType = ReservedPrefix + "dl-exception-type";

    /// <summary>
    /// W3C trace-context <c>traceparent</c>. Not reserved — it is a standard name, not an SDK-owned one. Stamped on
    /// send from the ambient producer <see cref="System.Diagnostics.Activity"/> and read back on receive to parent the
    /// consumer span.
    /// </summary>
    public const string TraceParent = "traceparent";

    /// <summary>W3C trace-context <c>tracestate</c>, carried alongside <see cref="TraceParent"/>.</summary>
    public const string TraceState = "tracestate";

    /// <summary>
    /// W3C <c>baggage</c> — user-defined context travelling with the trace. Neither stamped nor propagated by default;
    /// add it to an <see cref="IMessageHeaderPropagationPolicy"/> to opt in.
    /// </summary>
    public const string Baggage = "baggage";

    /// <summary>True when the key belongs to the SDK's reserved control namespace and is owned by the SDK, not the caller.</summary>
    /// <param name="key">The header key.</param>
    public static bool IsReserved(string key) => key is not null && key.StartsWith(ReservedPrefix, StringComparison.Ordinal);

    /// <summary>
    /// True when a transport adapter re-stamps this key from the envelope on every send, so a caller-supplied copy must
    /// be dropped rather than allowed to forge the wire contract.
    /// </summary>
    /// <remarks>
    /// Narrower than <see cref="IsReserved"/> on purpose. An adapter that strips the whole <c>wt-</c> namespace also
    /// strips the reserved headers SDK <em>features</em> stamp — the second-level-retry tier, the dead-letter redrive
    /// count, the claim-check reference — none of which the adapter re-stamps. Those then survive in-memory and vanish
    /// behind every broker, silently disabling the feature. Strip only what is re-derived below.
    /// </remarks>
    /// <param name="key">The header key.</param>
    public static bool IsAdapterOwned(string key) => key is not null && (
        key == EventType || key == ContentType || key == MessageId || key == PartitionKey ||
        key == ReplyTo || key == CorrelationId || key == ConversationId || key == DeliveryCount);
}

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
