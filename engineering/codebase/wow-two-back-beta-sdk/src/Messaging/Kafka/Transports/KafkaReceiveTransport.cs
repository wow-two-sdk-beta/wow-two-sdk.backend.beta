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
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Kafka.Transports;

/// <summary>
/// Transports events from Kafka by polling, reconstructing envelopes, and routing them into the
/// pipeline. Manual offset store (commit on ack). The subscription is the mirror image of the send path's topic
/// resolution: every routing key <see cref="ITopologyService"/> declares for this process, mapped through the same
/// <see cref="KafkaTopicNameMapper"/>.
/// </summary>
internal sealed partial class KafkaReceiveTransport(
    KafkaOptions options,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ITopologyService topology,
    ILogger<KafkaReceiveTransport> logger,
    IReplyAddressService? replyAddresses = null,
    MessageSerializerRegistry? serializerRegistry = null) : IReceiveTransport, IAsyncDisposable
{
    private IConsumer<string, byte[]>? _consumer;
    private IProducer<string, byte[]>? _deadLetterProducer;

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);
        var opt = options;

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

    /// <summary>Every topic this process consumes — <see cref="KafkaOptions.Topic"/> always, plus each routed topic when routing is on.</summary>
    private List<string> ResolveSubscription()
    {
        var opt = options;
        var topics = new List<string> { opt.Topic };
        if (!opt.RouteByDestination)
            return topics;

        // One topic per consumed type, plus the endpoint's own name so an addressed send or reply arrives.
        foreach (var endpoint in topology.ConsumeEndpoints)
            foreach (var routingKey in endpoint.RoutingKeys)
                AddTopic(topics, KafkaTopicNameMapper.From(routingKey));

        // A per-instance reply address is outside the topology — unsubscribed, every request times out.
        if (replyAddresses is not null)
            AddTopic(topics, KafkaTopicNameMapper.From(replyAddresses.ReplyAddress));

        return topics;

        static void AddTopic(List<string> topics, string? topic)
        {
            if (topic is { Length: > 0 } && !topics.Contains(topic, StringComparer.Ordinal))
                topics.Add(topic);
        }
    }

    // librdkafka's consumer is not thread-safe, so Consume and StoreOffset stay on this one thread.
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
                _deadLetterProducer!.ProduceAsync(options.DeadLetterTopic, KafkaDeadLetterMapper.Build(result.Message, "unparseable", null), cancellationToken)
                    .GetAwaiter().GetResult();
                _consumer!.StoreOffset(result);
                continue;
            }

            var context = new KafkaReceiveContext(envelope, _deadLetterProducer!, options.DeadLetterTopic, result.Message);
            try
            {
                // Process on the consume thread, then advance the offset here; success and dead-letter both advance.
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

    private EventEnvelopeModel? TryReconstruct(ConsumeResult<string, byte[]> result)
    {
        var headers = DecodeHeaders(result.Message.Headers);
        if (!headers.TryGetValue(MessageHeaderConstants.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        // Select on the declared content type or nothing — guessing JSON would route a legacy body to the wrong deserializer.
        var decoded = SerializerFor(ReadOptional(headers, MessageHeaderConstants.ContentType)).Deserialize(result.Message.Value, eventType);
        if (decoded.IsFailure(out _, out var body))
            return null;

        return new EventEnvelopeModel
        {
            MessageId = headers.TryGetValue(MessageHeaderConstants.MessageId, out var id) ? id : Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = result.Topic,
            PartitionKey = result.Message.Key,
            DeliveryCount = ReadDeliveryCount(headers),
            ContentType = headers.TryGetValue(MessageHeaderConstants.ContentType, out var contentType) ? contentType : "application/json",
            ReplyTo = ReadOptional(headers, MessageHeaderConstants.ReplyTo),
            CorrelationId = ReadOptional(headers, MessageHeaderConstants.CorrelationId),
            ConversationId = ReadOptional(headers, MessageHeaderConstants.ConversationId),
            Headers = headers,
        };
    }

    /// <summary>The delivery count the producer stamped, or 1 when no header is present.</summary>
    private static int ReadDeliveryCount(Dictionary<string, string> headers)
    {
        // Treat Kafka reconsumption as a first delivery because the wire record has no attempt count.
        if (!headers.TryGetValue(MessageHeaderConstants.DeliveryCount, out var raw))
            return 1;

        // A malformed or non-positive value counts as a first delivery, so a bad header cannot dead-letter on arrival.
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0 ? count : 1;
    }

    private static string? ReadOptional(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>The deserializer for a received content type. Falls back to the injected serializer when no registry is wired.</summary>
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
