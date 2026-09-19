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
/// Transports JSON-serialized events through a Kafka topic. The topic comes from
/// <see cref="ITopologyService"/> once <see cref="KafkaOptions.RouteByDestination"/> is on: the message type's stable
/// token for a publish, the caller's address for an explicit send.
/// </summary>
internal sealed class KafkaSendTransport(
    KafkaOptions options,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ITopologyService topology) : ISendTransport, IAsyncDisposable
{
    private readonly IProducer<string, byte[]> _producer =
        new ProducerBuilder<string, byte[]>(new ProducerConfig { BootstrapServers = options.BootstrapServers }).Build();

    public async ValueTask SendAsync(EventEnvelopeModel envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var body = envelope.ToWireBody(serializer);
        // Caller headers first, minus the adapter-owned wt-* namespace — a duplicate key would win the decode.
        var headers = new Headers();
        foreach (var (key, value) in envelope.Headers)
            if (!MessageHeaderConstants.IsAdapterOwned(key))
                headers.Add(key, Encoding.UTF8.GetBytes(value));

        // The type token names the shape actually on the wire, which is what the receiver deserializes into.
        headers.Add(MessageHeaderConstants.EventType, Encoding.UTF8.GetBytes(typeResolver.ToTypeToken(envelope.WireBodyType)));
        headers.Add(MessageHeaderConstants.ContentType, Encoding.UTF8.GetBytes(serializer.ContentType));
        headers.Add(MessageHeaderConstants.MessageId, Encoding.UTF8.GetBytes(envelope.MessageId));

        // Kafka has no reply, correlation or conversation property, so all three ride the reserved headers.
        if (!string.IsNullOrEmpty(envelope.ReplyTo))
            headers.Add(MessageHeaderConstants.ReplyTo, Encoding.UTF8.GetBytes(envelope.ReplyTo));
        if (!string.IsNullOrEmpty(envelope.CorrelationId))
            headers.Add(MessageHeaderConstants.CorrelationId, Encoding.UTF8.GetBytes(envelope.CorrelationId));
        if (!string.IsNullOrEmpty(envelope.ConversationId))
            headers.Add(MessageHeaderConstants.ConversationId, Encoding.UTF8.GetBytes(envelope.ConversationId));

        // Carry an advanced DeliveryCount onto the new record so the next consumer learns the attempt count.
        if (envelope.DeliveryCount > 0)
            headers.Add(MessageHeaderConstants.DeliveryCount, Encoding.UTF8.GetBytes(envelope.DeliveryCount.ToString(CultureInfo.InvariantCulture)));

        var message = new Message<string, byte[]>
        {
            // Null when no ordering key is set — librdkafka then batches on its sticky partitioner.
            Key = envelope.PartitionKey!,
            Value = body,
            Headers = headers,
        };

        await _producer.ProduceAsync(ResolveTopic(envelope), message, cancellationToken);
    }

    /// <summary>The topic this envelope is produced to — <see cref="KafkaOptions.Topic"/>, or the topology's resolved key when routing is on.</summary>
    private string ResolveTopic(EventEnvelopeModel envelope)
    {
        if (!options.RouteByDestination)
            return options.Topic;

        // A key that sanitizes away to nothing falls back to the configured topic, which every consumer subscribes to.
        return KafkaTopicNameMapper.From(topology.ResolveRoutingKey(envelope)) ?? options.Topic;
    }

    public ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        return ValueTask.CompletedTask;
    }
}
