using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus.Transports;

/// <summary>
/// Transports envelopes through an Azure Service Bus topic, mapping every transport-abstract
/// hint the envelope carries onto the Service Bus property that natively implements it: scheduled enqueue time, TTL,
/// session id, reply-to and correlation id are message properties, not headers. The <c>Subject</c> carries the routing
/// key <see cref="ITopologyService"/> resolves, which is what subscription correlation filters match on.
/// </summary>
internal sealed class AzureServiceBusSendTransport(
    AzureServiceBusConnection connection,
    AzureServiceBusOptions options,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ITopologyService topology) : ISendTransport, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ServiceBusSender? _sender;

    public async ValueTask SendAsync(EventEnvelopeModel envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var sender = await GetSenderAsync(cancellationToken);
        var body = envelope.ToWireBody(serializer);

        var message = new ServiceBusMessage(new BinaryData(body))
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            ContentType = serializer.ContentType,

            // Subject carries the raw topology key, which is what a subscription's correlation filter matches.
            Subject = topology.ResolveRoutingKey(envelope),

            // Native reply address, left null for a one-way message so the property is absent from the AMQP frame.
            ReplyTo = string.IsNullOrEmpty(envelope.ReplyTo) ? null : envelope.ReplyTo,
        };

        // Caller headers minus the reserved wt-* namespace: forwarding a consumed envelope's would misroute the body.
        foreach (var (key, value) in envelope.Headers)
            if (!MessageHeaderConstants.IsAdapterOwned(key))
                message.ApplicationProperties[key] = value;

        // WireBodyType is what the receiver deserializes into; a send-path transformation changes the wire shape.
        message.ApplicationProperties[AzureServiceBusHeaderConstants.EventType] = typeResolver.ToTypeToken(envelope.WireBodyType);
        message.ApplicationProperties[AzureServiceBusHeaderConstants.ContentType] = serializer.ContentType;

        if (!string.IsNullOrEmpty(envelope.ConversationId))
            message.ApplicationProperties[AzureServiceBusHeaderConstants.ConversationId] = envelope.ConversationId;

        ApplyOrderingKey(message, envelope);

        // Native per-message TTL: Service Bus clamps it to the entity default, and a non-positive hint is dropped.
        if (envelope.TimeToLive is { } timeToLive && timeToLive > TimeSpan.Zero)
            message.TimeToLive = timeToLive;

        // Native scheduled delivery: the broker holds the message until NotBeforeUtc; a time already past is dropped.
        if (envelope.NotBeforeUtc is { } notBefore && notBefore > DateTimeOffset.UtcNow)
            message.ScheduledEnqueueTime = notBefore;

        // The client retries transient faults internally; a resend after an ambiguous failure duplicates the message.
        await sender.SendMessageAsync(message, cancellationToken);
    }

    /// <summary>
    /// Map the envelope's ordering key onto whichever Service Bus property actually orders on this entity.
    /// </summary>
    /// <remarks>
    ///   - on a session entity <c>SessionId</c> is the partition key
    ///   - setting a differing <c>PartitionKey</c> there is rejected
    ///   - on a session-unaware entity only <c>PartitionKey</c> groups onto a partition
    /// </remarks>
    private void ApplyOrderingKey(ServiceBusMessage message, EventEnvelopeModel envelope)
    {
        if (string.IsNullOrEmpty(envelope.PartitionKey))
            return;

        if (options.RequiresSession)
            message.SessionId = envelope.PartitionKey;
        else
            message.PartitionKey = envelope.PartitionKey;

        message.ApplicationProperties[AzureServiceBusHeaderConstants.PartitionKey] = envelope.PartitionKey;
    }

    private async ValueTask<ServiceBusSender> GetSenderAsync(CancellationToken cancellationToken)
    {
        if (_sender is { IsClosed: false })
            return _sender;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_sender is { IsClosed: false })
                return _sender;

            var client = await connection.GetClientAsync(cancellationToken);
            var topic = AzureServiceBusEntityNameMapper.Sanitize(options.Topic, AzureServiceBusEntityNameMapper.MaxTopicLength);

            // The sender is cached for the application's life: it owns the AMQP link and reopens it after a drop.
            _sender = client.CreateSender(topic);
            return _sender;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender is not null)
            await _sender.DisposeAsync();
        _gate.Dispose();
    }
}
