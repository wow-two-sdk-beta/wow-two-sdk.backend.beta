using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq.Transports;

/// <summary>
/// Transports envelopes through a RabbitMQ topic exchange on a channel running in
/// publisher-confirm mode, so a send completes only once the broker has durably accepted it. The routing key comes
/// from <see cref="ITopologyService"/>: the message type's stable token for a publish, the caller's address for an
/// explicit send.
/// </summary>
internal sealed partial class RabbitMqSendTransport(
    RabbitMqConnection connection,
    RabbitMqOptions options,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ITopologyService topology,
    ILogger<RabbitMqSendTransport> logger) : ISendTransport, IAsyncDisposable
{
    /// <summary>
    /// Ceiling on publishes awaiting a broker ack on the channel. This is the client's own documented default; the
    /// 7.0.0 constructor parameter nonetheless defaults to <c>null</c> (no limit), so it has to be passed explicitly
    /// or unacked publishes accumulate without bound. It throttles admission past the threshold, it does not
    /// serialize: publishes stay concurrent, each awaiting its own confirm.
    /// </summary>
    private const int MaxOutstandingConfirms = 128;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    /// <summary>
    /// Confirm mode is a channel-creation option in RabbitMQ.Client 7.x — there is no 6.x-style <c>ConfirmSelect</c> /
    /// <c>WaitForConfirmsOrDie</c> pair on the channel. With tracking on, <c>BasicPublishAsync</c> itself completes
    /// only once the broker acks the publish and throws <c>PublishException</c> on a nack, so awaiting the publish
    /// <em>is</em> the confirm wait. Built per channel: the channel disposes the rate limiter handed to it.
    /// </summary>
    private static CreateChannelOptions ConfirmChannelOptions() => new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true,
        outstandingPublisherConfirmationsRateLimiter: new ThrottlingRateLimiter(MaxOutstandingConfirms));

    public async ValueTask SendAsync(EventEnvelopeModel envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var channel = await GetChannelAsync(cancellationToken);
        var body = envelope.ToWireBody(serializer);

        // Caller headers first, minus the reserved namespace, then the adapter's own wt-* control headers.
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in envelope.Headers)
            if (!MessageHeaderConstants.IsAdapterOwned(key))
                headers[key] = value;

        // WireBodyType, not BodyType — the receiver deserializes into this token; routing below stays on BodyType.
        headers[MessageHeaderConstants.EventType] = typeResolver.ToTypeToken(envelope.WireBodyType);
        headers[MessageHeaderConstants.ContentType] = serializer.ContentType;
        if (!string.IsNullOrEmpty(envelope.PartitionKey))
            headers[MessageHeaderConstants.PartitionKey] = envelope.PartitionKey;

        // The conversation id rides a header — correlation-id already carries EventEnvelopeModel.CorrelationId.
        if (!string.IsNullOrEmpty(envelope.ConversationId))
            headers[MessageHeaderConstants.ConversationId] = envelope.ConversationId;

        var properties = new BasicProperties
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,

            // Native AMQP reply-to; null on a one-way message leaves the property absent rather than empty.
            ReplyTo = string.IsNullOrEmpty(envelope.ReplyTo) ? null : envelope.ReplyTo,
            Persistent = envelope.Durable,
            Headers = headers,
        };

        // Clamp the envelope's int hint into the AMQP byte range, and stamp it only when the caller set one.
        if (envelope.Priority is { } priority)
            properties.Priority = (byte)Math.Clamp(priority, byte.MinValue, byte.MaxValue);

        // Per-message TTL is a millisecond count in a string; floor negatives to 0, which the channel accepts.
        if (envelope.TimeToLive is { } timeToLive)
            properties.Expiration = Math.Max(0, (long)timeToLive.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

        // Returns once the broker acks; a nack throws PublishException — see RabbitMq.md § Publish semantics.
        await channel.BasicPublishAsync(options.Exchange, topology.ResolveRoutingKey(envelope), mandatory: false, properties, body, cancellationToken);
    }

    private async ValueTask<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        // The gate covers channel creation only — concurrent publishes share the channel and await their own ack.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return _channel;

            // Release a channel that closed under us before replacing it, so its confirm state goes away.
            if (_channel is { } stale)
            {
                _channel = null;
                try
                {
                    await stale.DisposeAsync();
                }
                catch (Exception ex)
                {
                    // Best-effort: the channel is already gone (connection down), which is why it is being replaced.
                    LogStaleChannelDisposeFailed(ex);
                }
            }

            var conn = await connection.GetConnectionAsync(cancellationToken);
            var channel = await conn.CreateChannelAsync(ConfirmChannelOptions(), cancellationToken);
            await channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
            _channel = channel;
            return channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        _gate.Dispose();
    }

    [LoggerMessage(EventId = 6302, Level = LogLevel.Debug, Message = "Disposing the closed RabbitMQ publish channel failed; replacing it anyway")]
    private partial void LogStaleChannelDisposeFailed(Exception exception);
}
