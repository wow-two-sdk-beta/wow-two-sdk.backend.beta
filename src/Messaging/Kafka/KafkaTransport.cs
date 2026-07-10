using System.Reflection;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
internal sealed class KafkaSendTransport(IOptions<KafkaOptions> options) : ISendTransport, IAsyncDisposable
{
    private readonly IProducer<string, byte[]> _producer =
        new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = options.Value.BootstrapServers }).Build();

    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope.Body, envelope.BodyType);
        var headers = new Headers
        {
            { KafkaHeaders.EventType, Encoding.UTF8.GetBytes(envelope.BodyType.AssemblyQualifiedName ?? envelope.BodyType.FullName ?? string.Empty) },
            { KafkaHeaders.MessageId, Encoding.UTF8.GetBytes(envelope.MessageId) },
        };
        foreach (var (key, value) in envelope.Headers)
            headers.Add(key, Encoding.UTF8.GetBytes(value));

        var message = new Message<string, byte[]>
        {
            Key = envelope.PartitionKey ?? envelope.MessageId,
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
internal sealed partial class KafkaReceiveTransport(IOptions<KafkaOptions> options, ILogger<KafkaReceiveTransport> logger) : IReceiveTransport, IAsyncDisposable
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

    private static EventEnvelope? TryReconstruct(ConsumeResult<string, byte[]> result)
    {
        var headers = DecodeHeaders(result.Message.Headers);
        if (!headers.TryGetValue(KafkaHeaders.EventType, out var typeName) || Type.GetType(typeName) is not { } eventType)
            return null;

        var body = JsonSerializer.Deserialize(result.Message.Value, eventType);
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

/// <summary>Kafka capability flags — no native DLQ (SDK emulates via a dead-letter topic); per-partition ordering.</summary>
internal sealed class KafkaCapabilities : ITransportCapabilities
{
    public bool NativeDeadLetter => false;

    public bool NativeDelay => false;

    public bool NativeDedupe => false;

    public bool NativeOrdering => true;
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
