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
}

/// <summary>Well-known Kafka header keys.</summary>
internal static class KafkaHeaders
{
    public const string EventType = "wt-event-type";
    public const string ContentType = "wt-content-type";
    public const string MessageId = "wt-message-id";
    public const string DeadLetterReason = "wt-dl-reason";
    public const string DeadLetterExceptionType = "wt-dl-exception-type";
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
        headers.Add(KafkaHeaders.DeadLetterReason, Encoding.UTF8.GetBytes(reason));
        if (exception is not null)
            headers.Add(KafkaHeaders.DeadLetterExceptionType, Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name));
        return new Message<string, byte[]> { Key = original.Key, Value = original.Value, Headers = headers };
    }
}

/// <summary>Kafka <see cref="ISendTransport"/> — produces JSON-serialized events to a topic (key = partition/message id).</summary>
internal sealed class KafkaSendTransport(
    IOptions<KafkaOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver) : ISendTransport, IAsyncDisposable
{
    private readonly IProducer<string, byte[]> _producer =
        new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = options.Value.BootstrapServers }).Build();

    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var body = serializer.Serialize(envelope.Body, envelope.BodyType);
        // Caller headers first, minus the reserved namespace, then the control headers — the adapter owns wt-*. Kafka
        // headers allow duplicate keys, so an unfiltered caller header would ride the wire alongside the real one and win
        // the decode (last key into the dictionary). A received envelope carries wt-* keys, so a re-publish would stamp
        // the previous message's type token onto the new body.
        var headers = new Headers();
        foreach (var (key, value) in envelope.Headers)
            if (!MessageHeaders.IsReserved(key))
                headers.Add(key, Encoding.UTF8.GetBytes(value));

        headers.Add(KafkaHeaders.EventType, Encoding.UTF8.GetBytes(typeResolver.ToTypeToken(envelope.BodyType)));
        headers.Add(KafkaHeaders.ContentType, Encoding.UTF8.GetBytes(serializer.ContentType));
        headers.Add(KafkaHeaders.MessageId, Encoding.UTF8.GetBytes(envelope.MessageId));

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

        await _producer.ProduceAsync(options.Value.Topic, message, cancellationToken);
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

/// <summary>Kafka <see cref="IReceiveTransport"/> — subscribes to the topic, polls, reconstructs envelopes, and routes them into the pipeline. Manual offset store (commit on ack).</summary>
internal sealed partial class KafkaReceiveTransport(
    IOptions<KafkaOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ILogger<KafkaReceiveTransport> logger) : IReceiveTransport, IAsyncDisposable
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
        _consumer.Subscribe(opt.Topic);

        await Task.Run(() => ConsumeLoop(onMessage, cancellationToken), cancellationToken);
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
        if (!headers.TryGetValue(KafkaHeaders.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        var body = serializer.Deserialize(result.Message.Value, eventType);
        if (body is null)
            return null;

        return new EventEnvelope
        {
            MessageId = headers.TryGetValue(KafkaHeaders.MessageId, out var id) ? id : Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = result.Topic,
            PartitionKey = result.Message.Key,
            DeliveryCount = 1, // Kafka has no native per-message redelivery count; true tracking needs a bumped header (follow-up)
            ContentType = headers.TryGetValue(KafkaHeaders.ContentType, out var contentType) ? contentType : "application/json",
            Headers = headers,
        };
    }

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
    /// the shared <see cref="IEventBus"/>, resilience defaults, and the consumer hosted service. Scans the supplied
    /// assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>.
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

        services.TryAddSingleton<ITransportCapabilities, KafkaCapabilities>();
        services.TryAddSingleton<ISendTransport, KafkaSendTransport>();
        services.TryAddSingleton<IReceiveTransport, KafkaReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerHostedService>();
        return services;
    }
}
