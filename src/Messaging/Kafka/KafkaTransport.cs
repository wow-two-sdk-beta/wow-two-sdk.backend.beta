using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Kafka;

/// <summary>Options for the Kafka event-bus adapter.</summary>
public sealed class KafkaOptions
{
    /// <summary>Bootstrap servers (host:port[,host:port]). Default local.</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Topic events are produced to / consumed from. Default <c>wt.events</c>.</summary>
    public string Topic { get; set; } = "wt.events";

    /// <summary>Consumer group id. Default <c>wt-consumers</c>.</summary>
    public string GroupId { get; set; } = "wt-consumers";

    /// <summary>Dead-letter topic — Kafka has no native DLQ, so exhausted/poison messages are re-produced here. Default <c>wt.events.dlq</c>.</summary>
    public string DeadLetterTopic { get; set; } = "wt.events.dlq";

    /// <summary>
    /// Route each message to the topic <see cref="ITopologyProvider"/> resolves from it — the message type's stable
    /// token for a publish, <see cref="EventEnvelope.Destination"/> for an explicit
    /// <see cref="IEventBus.SendAsync{TEvent}"/> — instead of producing everything to <see cref="Topic"/>. Default
    /// false, which keeps an existing deployment on its single topic.
    /// <para>
    /// Opt-in because it changes which topic a message lands on, and a consumer still on the old build subscribes to
    /// <see cref="Topic"/> alone. Migration is therefore consumers first: a consumer with this on subscribes to
    /// <see cref="Topic"/> <em>as well as</em> every routed topic, so it keeps receiving from producers that have not
    /// been switched yet. Turn it on for the producers once every consumer is across.
    /// </para>
    /// </summary>
    public bool RouteByDestination { get; set; }
}

/// <summary>
/// Maps a topology routing key onto a legal Kafka topic name. Kafka accepts <c>[A-Za-z0-9._-]</c> up to 249
/// characters and rejects <c>.</c> / <c>..</c> outright, which is narrower than the routing keys a custom
/// <see cref="ITopologyProvider"/> may return — so the key is sanitized rather than trusted.
/// </summary>
/// <remarks>
/// Both halves of the adapter call this on the same keys, so the topic the send path produces to is by construction
/// one the receive path subscribes to.
/// </remarks>
internal static class KafkaTopicName
{
    /// <summary>Kafka's own ceiling on a topic name.</summary>
    private const int MaxLength = 249;

    /// <summary>The topic for <paramref name="routingKey"/>, or null when nothing addressable survives sanitizing.</summary>
    public static string? From(string? routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
            return null;

        var builder = new StringBuilder(Math.Min(routingKey.Length, MaxLength));
        foreach (var character in routingKey)
        {
            if (builder.Length == MaxLength)
                break;

            // Deliberately an ASCII test, not char.IsLetterOrDigit: the latter admits non-ASCII letters, which Kafka
            // rejects. Everything else collapses to '-' so two distinct keys stay distinct topics.
            var legal = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_';
            builder.Append(legal ? character : '-');
        }

        var topic = builder.ToString().Trim('.');
        return topic.Length == 0 ? null : topic;
    }
}

/// <summary>Builds a dead-letter Kafka message — preserves the original key/value/headers and stamps death headers (reason + exception type) for triage.</summary>
internal static class KafkaDeadLetter
{
    public static Message<string, byte[]> Build(Message<string, byte[]> original, string reason, Exception? exception)
    {
        var headers = new Headers();
        if (original.Headers is not null)
            foreach (var header in original.Headers)
                headers.Add(header.Key, header.GetValueBytes());
        headers.Add(MessageHeaders.DeadLetterReason, Encoding.UTF8.GetBytes(reason));
        if (exception is not null)
            headers.Add(MessageHeaders.DeadLetterExceptionType, Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name));
        return new Message<string, byte[]> { Key = original.Key, Value = original.Value, Headers = headers };
    }
}

/// <summary>
/// Kafka <see cref="ISendTransport"/> — produces JSON-serialized events to a topic. The topic comes from
/// <see cref="ITopologyProvider"/> once <see cref="KafkaOptions.RouteByDestination"/> is on: the message type's stable
/// token for a publish, the caller's address for an explicit send.
/// </summary>
internal sealed class KafkaSendTransport(
    IOptions<KafkaOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ITopologyProvider topology) : ISendTransport, IAsyncDisposable
{
    private readonly IProducer<string, byte[]> _producer =
        new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = options.Value.BootstrapServers }).Build();

    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var body = envelope.ToWireBody(serializer);
        // Caller headers first, minus the reserved namespace, then the control headers — the adapter owns wt-*. Kafka
        // headers allow duplicate keys, so an unfiltered caller header would ride the wire alongside the real one and win
        // the decode (last key into the dictionary). A received envelope carries wt-* keys, so a re-publish would stamp
        // the previous message's type token onto the new body.
        var headers = new Headers();
        foreach (var (key, value) in envelope.Headers)
            if (!MessageHeaders.IsAdapterOwned(key))
                headers.Add(key, Encoding.UTF8.GetBytes(value));

        // WireBodyType, not BodyType: a send-path transformation that substituted the wire bytes decodes as a different
        // shape, and this token is what the receiver deserializes into. Topic resolution below stays on BodyType.
        headers.Add(MessageHeaders.EventType, Encoding.UTF8.GetBytes(typeResolver.ToTypeToken(envelope.WireBodyType)));
        headers.Add(MessageHeaders.ContentType, Encoding.UTF8.GetBytes(serializer.ContentType));
        headers.Add(MessageHeaders.MessageId, Encoding.UTF8.GetBytes(envelope.MessageId));

        // Kafka has no reply-address, correlation or conversation property, so all three ride the SDK's reserved
        // headers. Only stamped when set, so ordinary one-way traffic carries exactly the headers it did before.
        if (!string.IsNullOrEmpty(envelope.ReplyTo))
            headers.Add(MessageHeaders.ReplyTo, Encoding.UTF8.GetBytes(envelope.ReplyTo));
        if (!string.IsNullOrEmpty(envelope.CorrelationId))
            headers.Add(MessageHeaders.CorrelationId, Encoding.UTF8.GetBytes(envelope.CorrelationId));
        if (!string.IsNullOrEmpty(envelope.ConversationId))
            headers.Add(MessageHeaders.ConversationId, Encoding.UTF8.GetBytes(envelope.ConversationId));

        // A first publish is DeliveryCount 0 and stamps nothing; a re-produced envelope (delayed retry, DLQ replay)
        // arrives with the count already advanced and carries it onto the new record, which is the only way the next
        // consumer can learn how many attempts this message has had.
        if (envelope.DeliveryCount > 0)
            headers.Add(MessageHeaders.DeliveryCount, Encoding.UTF8.GetBytes(envelope.DeliveryCount.ToString(CultureInfo.InvariantCulture)));

        var message = new Message<string, byte[]>
        {
            // Null when the producer set no ordering key: a fabricated key (previously the message id) came back on the
            // consumer as a PartitionKey nobody chose, and made every message uniquely keyed to the pump and to metrics.
            // A null key also lets librdkafka's sticky partitioner batch, which a unique-per-message key defeats.
            // null-forgiving: Confluent's TKey carries no nullable annotation, but a null key is valid on the wire and is
            // what selects the sticky partitioner.
            Key = envelope.PartitionKey!,
            Value = body,
            Headers = headers,
        };

        await _producer.ProduceAsync(ResolveTopic(envelope), message, cancellationToken);
    }

    /// <summary>
    /// The topic this envelope is produced to. Off (the default) every message rides
    /// <see cref="KafkaOptions.Topic"/>, exactly as before. On, the topology decides — and it is the topology, not the
    /// raw destination, for the same reason the RabbitMQ adapter routes on the resolved key: a publish has to land on
    /// the type's own topic, which is what a consumer of that type subscribes to, while an explicit send has to land on
    /// the addressed endpoint's topic so it reaches that endpoint alone.
    /// </summary>
    private string ResolveTopic(EventEnvelope envelope)
    {
        if (!options.Value.RouteByDestination)
            return options.Value.Topic;

        // A key that sanitizes away to nothing would otherwise produce to an unnamed topic; the configured topic is
        // the one address every consumer of this adapter is subscribed to, so it is the safe floor.
        return KafkaTopicName.From(topology.ResolveRoutingKey(envelope)) ?? options.Value.Topic;
    }

    public ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Kafka <see cref="ReceiveContext"/>. Offset advancement (<c>StoreOffset</c>) is done by the consume loop on the
/// consume thread — librdkafka's consumer is not thread-safe — so acknowledge is a no-op here; dead-letter re-produces
/// the original message to the DLQ topic via the (thread-safe) producer (emulated DLQ).
/// </summary>
internal sealed class KafkaReceiveContext(
    EventEnvelope envelope,
    IProducer<string, byte[]> deadLetterProducer,
    string deadLetterTopic,
    Message<string, byte[]> originalMessage) : ReceiveContext
{
    public override EventEnvelope Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public override async ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
    {
        // Produce a fresh message (don't re-submit the consumed instance) carrying death headers for triage.
        await deadLetterProducer.ProduceAsync(deadLetterTopic, KafkaDeadLetter.Build(originalMessage, reason, exception), cancellationToken);
    }
}

/// <summary>
/// Kafka <see cref="IReceiveTransport"/> — subscribes, polls, reconstructs envelopes, and routes them into the
/// pipeline. Manual offset store (commit on ack). The subscription is the mirror image of the send path's topic
/// resolution: every routing key <see cref="ITopologyProvider"/> declares for this process, mapped through the same
/// <see cref="KafkaTopicName"/>.
/// </summary>
internal sealed partial class KafkaReceiveTransport(
    IOptions<KafkaOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ITopologyProvider topology,
    ILogger<KafkaReceiveTransport> logger,
    IReplyAddressProvider? replyAddresses = null,
    MessageSerializerRegistry? serializerRegistry = null) : IReceiveTransport, IAsyncDisposable
{
    private IConsumer<string, byte[]>? _consumer;
    private IProducer<string, byte[]>? _deadLetterProducer;

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);
        var opt = options.Value;

        _deadLetterProducer = new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = opt.BootstrapServers }).Build();
        _consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = opt.BootstrapServers,
            GroupId = opt.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false,
        }).Build();

        var topics = ResolveSubscription();
        LogSubscribed(string.Join(", ", topics));
        _consumer.Subscribe(topics);

        await Task.Run(() => ConsumeLoop(onMessage, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Every topic this process consumes. <see cref="KafkaOptions.Topic"/> is always in the set — with routing off it
    /// is the only one, and with routing on it is what keeps a producer that has not been switched yet reachable,
    /// which is what makes a consumers-first rollout possible.
    /// </summary>
    /// <remarks>
    /// The routed part is <see cref="EndpointTopology.RoutingKeys"/>, the same list the RabbitMQ adapter binds to its
    /// queue: one key per consumed message type (so a publish of that type arrives) plus the endpoint's own name (so an
    /// explicit send addressed to this endpoint — a reply, above all — arrives). Kafka has no exchange to bind
    /// through, so the key set becomes a topic set instead. Anything the send path can resolve for a type this process
    /// handles, or for an address it owns, is therefore already here.
    /// </remarks>
    private List<string> ResolveSubscription()
    {
        var opt = options.Value;
        var topics = new List<string> { opt.Topic };
        if (!opt.RouteByDestination)
            return topics;

        foreach (var endpoint in topology.ConsumeEndpoints)
            foreach (var routingKey in endpoint.RoutingKeys)
                AddTopic(topics, KafkaTopicName.From(routingKey));

        // A configured per-instance reply address is not part of the topology — it is this process's alone — so the
        // topology cannot know about it. Without this the request client would stamp a ReplyTo nothing consumes and
        // every request would time out, which is the whole point of addressing a reply.
        if (replyAddresses is not null)
            AddTopic(topics, KafkaTopicName.From(replyAddresses.ReplyAddress));

        return topics;

        static void AddTopic(List<string> topics, string? topic)
        {
            if (topic is { Length: > 0 } && !topics.Contains(topic, StringComparer.Ordinal))
                topics.Add(topic);
        }
    }

    // Deliberately single-threaded: librdkafka's consumer is not thread-safe, so Consume + StoreOffset stay on this one
    // thread. KafkaCapabilities.ThreadAffineConsume = true is what tells the shared concurrency pump to keep dispatch
    // inline here; parallel handler dispatch would require marshalling offset stores back to this loop.
    private void ConsumeLoop(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, byte[]>? result;
            try
            {
                result = _consumer!.Consume(TimeSpan.FromMilliseconds(500));
            }
            catch (ConsumeException ex)
            {
                LogConsumeError(ex);
                continue;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (result is null || result.IsPartitionEOF)
                continue;

            var envelope = TryReconstruct(result);
            if (envelope is null)
            {
                LogUnparseable();
                // Don't silently drop — re-produce the raw message to the DLQ topic (so it's recoverable), then advance.
                _deadLetterProducer!.ProduceAsync(options.Value.DeadLetterTopic, KafkaDeadLetter.Build(result.Message, "unparseable", null), cancellationToken)
                    .GetAwaiter().GetResult();
                _consumer!.StoreOffset(result);
                continue;
            }

            var context = new KafkaReceiveContext(envelope, _deadLetterProducer!, options.Value.DeadLetterTopic, result.Message);
            try
            {
                // Process synchronously on the consume thread, then advance the offset here — Consume + StoreOffset
                // must share one thread (librdkafka is not thread-safe). Success and dead-letter both advance.
                onMessage(context, cancellationToken).AsTask().GetAwaiter().GetResult();
                _consumer!.StoreOffset(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogProcessingError(ex); // offset not stored → message is re-consumed (deduped by the inbox)
            }
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken) => DisposeAsync();

    private EventEnvelope? TryReconstruct(ConsumeResult<string, byte[]> result)
    {
        var headers = DecodeHeaders(result.Message.Headers);
        if (!headers.TryGetValue(MessageHeaders.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        // Decoded by whoever encoded it. Selection reads "declared or nothing", never the "application/json" the
        // envelope below falls back to: a producer that stamped no content type is one whose format we do not know, and
        // guessing JSON for it would route a legacy body to the wrong deserializer wherever the default is not JSON.
        var body = SerializerFor(ReadOptional(headers, MessageHeaders.ContentType)).Deserialize(result.Message.Value, eventType);
        if (body is null)
            return null;

        return new EventEnvelope
        {
            MessageId = headers.TryGetValue(MessageHeaders.MessageId, out var id) ? id : Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = result.Topic,
            PartitionKey = result.Message.Key,
            DeliveryCount = ReadDeliveryCount(headers),
            ContentType = headers.TryGetValue(MessageHeaders.ContentType, out var contentType) ? contentType : "application/json",
            ReplyTo = ReadOptional(headers, MessageHeaders.ReplyTo),
            CorrelationId = ReadOptional(headers, MessageHeaders.CorrelationId),
            ConversationId = ReadOptional(headers, MessageHeaders.ConversationId),
            Headers = headers,
        };
    }

    /// <summary>
    /// The delivery count the producer stamped, or 1 for a record nothing has re-produced. Deliberately trusts only the
    /// header: a record re-consumed because its offset was never stored comes back byte-identical, so it cannot be
    /// distinguished from a first delivery from the wire alone, and the process-local state that could tell them apart
    /// dies in the restart or rebalance that caused the redelivery.
    /// </summary>
    private static int ReadDeliveryCount(Dictionary<string, string> headers)
    {
        if (!headers.TryGetValue(MessageHeaders.DeliveryCount, out var raw))
            return 1;

        // A malformed or non-positive value is treated as a first delivery rather than trusted: the count only ever
        // drives give-up decisions, and a bad header must not be able to dead-letter a healthy message on arrival.
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0 ? count : 1;
    }

    private static string? ReadOptional(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>The deserializer for a received content type. Falls back to the injected serializer whenever no registry is wired, which is what keeps a single-serializer container behaving exactly as it did.</summary>
    private IMessageSerializer SerializerFor(string? contentType) => serializerRegistry?.Resolve(contentType) ?? serializer;

    private static Dictionary<string, string> DecodeHeaders(Headers headers)
    {
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
            return decoded;

        foreach (var header in headers)
            decoded[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());

        return decoded;
    }

    public ValueTask DisposeAsync()
    {
        if (_consumer is { } consumer)
        {
            consumer.Close();
            consumer.Dispose();
            _consumer = null;
        }

        if (_deadLetterProducer is { } producer)
        {
            producer.Flush(TimeSpan.FromSeconds(5));
            producer.Dispose();
            _deadLetterProducer = null;
        }

        return ValueTask.CompletedTask;
    }

    [LoggerMessage(EventId = 6401, Level = LogLevel.Error, Message = "Kafka consume error")]
    private partial void LogConsumeError(Exception exception);

    [LoggerMessage(EventId = 6402, Level = LogLevel.Warning, Message = "Discarding unparseable Kafka message")]
    private partial void LogUnparseable();

    [LoggerMessage(EventId = 6403, Level = LogLevel.Error, Message = "Kafka message processing failed; offset not advanced (will re-consume)")]
    private partial void LogProcessingError(Exception exception);

    [LoggerMessage(EventId = 6404, Level = LogLevel.Information, Message = "Subscribing to Kafka topics {Topics}")]
    private partial void LogSubscribed(string topics);
}

/// <summary>
/// Kafka capability flags — no native DLQ (SDK emulates via a dead-letter topic); per-partition ordering; broker acks
/// and producer transactions (EOS); no per-message priority, TTL, scheduling, sessions or reply-address mechanism;
/// consume/settle is thread-affine to the consume loop, so dispatch stays sequential.
/// </summary>
internal sealed class KafkaCapabilities : ITransportCapabilities
{
    public bool NativeDeadLetter => false;

    public bool NativeDelay => false;

    public bool NativeDedupe => false;

    public bool NativeOrdering => true;

    /// <summary>Kafka has no per-message priority on the wire at all — a partition is a strict append-only log, so a later message can never overtake an earlier one.</summary>
    public bool NativePriority => false;

    /// <summary>
    /// Kafka expires by topic-wide retention (<c>retention.ms</c> / <c>retention.bytes</c>), which ages every record in
    /// the topic on one shared clock. There is no per-record expiry, so an envelope's TimeToLive cannot be honoured.
    /// </summary>
    public bool NativeTimeToLive => false;

    /// <summary>No broker-side scheduled delivery: a produced record is immediately fetchable, and holding it back would have to be emulated consumer-side.</summary>
    public bool NativeScheduling => false;

    /// <summary>
    /// Partition keys give ordering (already reported by NativeOrdering) but not sessions — there is no exclusive
    /// per-group consumer lock or group-scoped state; a rebalance can hand a key's partition to another consumer.
    /// </summary>
    public bool NativeSessions => false;

    /// <summary>The <c>acks</c> setting plus the offset returned from produce is a real broker acknowledgement of durable acceptance.</summary>
    public bool NativePublisherConfirms => true;

    /// <summary>
    /// Kafka EOS — <c>transactional.id</c> with begin/commit and offsets committed inside the transaction. The
    /// capability is native; the adapter's producer is not configured with a <c>transactional.id</c> today, so nothing
    /// currently opens a transaction.
    /// </summary>
    public bool NativeTransactions => true;

    /// <summary>No reply-address mechanism: request-reply over Kafka means provisioning a reply topic and correlating by header, which is the SDK's work, not the broker's.</summary>
    public bool NativeRequestReply => false;

    /// <summary>Offsets advance on the consume loop, not through the receive context — a worker thread must not settle out of band.</summary>
    public bool SettlesInContext => false;

    /// <summary>librdkafka's consumer is not thread-safe: <c>Consume</c> and <c>StoreOffset</c> must stay on the loop thread, so the pump keeps dispatch sequential.</summary>
    public bool ThreadAffineConsume => true;
}

/// <summary>DI registration for the Kafka event-bus adapter.</summary>
public static class KafkaServiceCollectionExtensions
{
    /// <summary>
    /// Register the Kafka transport behind the SDK event bus: send/receive transports (topic + emulated DLQ topic),
    /// the shared <see cref="IEventBus"/>, resilience defaults, topology, and the consumer hosted service. Scans the
    /// supplied assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>.
    /// <para>
    /// Set <see cref="KafkaOptions.RouteByDestination"/> to route each message to its own topic; the topology derived
    /// from the handler scan is then what the consumer subscribes to. To change the endpoint shape, call
    /// <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> with its options before this — the
    /// two calls share one consumed-type set, and only the names this method back-fills from
    /// <see cref="KafkaOptions"/> are left to it.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Kafka options (bootstrap servers, topic, group, DLQ topic).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddKafkaEventBus(
        this IServiceCollection services,
        Action<KafkaOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<KafkaOptions>().Configure(configure);

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // After the handler scan, never before: the consumed-type set that becomes the routed topics is read out of
        // the registered IEventHandler<> descriptors, and a type registered later gets no topic.
        services.AddMessageTopology();

        // The shared endpoint's name is this adapter's topic, so an explicit send addressed to this process (a reply)
        // resolves to the topic it already consumes. Null-coalesce rather than assign — an explicit
        // AddMessageTopology(...) still wins.
        services.AddOptions<TopologyOptions>().PostConfigure<IOptions<KafkaOptions>>((topology, kafka) =>
        {
            topology.SharedEndpointName ??= kafka.Value.Topic;
            topology.SharedDeadLetterQueueName ??= kafka.Value.DeadLetterTopic;
        });

        services.TryAddSingleton<ITransportCapabilities, KafkaCapabilities>();
        services.TryAddSingleton<ISendTransport, KafkaSendTransport>();
        services.TryAddSingleton<IReceiveTransport, KafkaReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerHostedService>();
        return services;
    }
}
