using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq;

/// <summary>Options for the RabbitMQ event-bus adapter.</summary>
public sealed class RabbitMqOptions
{
    /// <summary>AMQP connection URI. Default local guest.</summary>
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672/";

    /// <summary>Topic exchange events are published to. Default <c>wt.events</c>.</summary>
    public string Exchange { get; set; } = "wt.events";

    /// <summary>This service's queue. Default <c>wt.events.queue</c>.</summary>
    public string Queue { get; set; } = "wt.events.queue";

    /// <summary>Dead-letter exchange (native DLQ). Default <c>wt.events.dlx</c>.</summary>
    public string DeadLetterExchange { get; set; } = "wt.events.dlx";

    /// <summary>Dead-letter queue. Default <c>wt.events.dlq</c>.</summary>
    public string DeadLetterQueue { get; set; } = "wt.events.dlq";

    /// <summary>
    /// Consumer prefetch (unacked in flight). Default 10. Keep it at least
    /// <see cref="ConcurrencyOptions.MaxConcurrentMessages"/>: the broker never leaves more than this many messages
    /// unacked, so a smaller prefetch becomes the effective concurrency limit however many pump workers are configured.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>
    /// Wait between automatic reconnect attempts after the connection drops. Default 5s. The client retries
    /// indefinitely at this interval, so this is the floor on how long a recovered broker stays unnoticed.
    /// </summary>
    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>Well-known RabbitMQ header keys the adapter sets.</summary>
internal static class RabbitMqHeaders
{
    /// <summary>Carries the event's stable type token so the consumer can resolve the CLR type.</summary>
    public const string EventType = "wt-event-type";

    /// <summary>Carries the serializer content type so the consumer selects the matching deserializer.</summary>
    public const string ContentType = "wt-content-type";

    /// <summary>Carries the ordering / partition key so the consumer can preserve per-key ordering.</summary>
    public const string PartitionKey = "wt-partition-key";
}

/// <summary>Lazily-opened shared RabbitMQ connection (singleton), with automatic connection + topology recovery.</summary>
internal sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        // An existing connection is returned even when IsOpen is false. With automatic recovery on, a closed
        // connection is a transient reconnect window, not a dead object — the client restores it in place and re-runs
        // topology recovery on it. Gating on IsOpen here would open a second connection on every network blip and
        // leak the recovering one, which is worse than the outage it reacts to. Callers see the outage as a failure
        // to open a channel, which the receive transport retries and the send transport surfaces.
        if (_connection is not null)
            return _connection;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null)
                return _connection;

            var factory = new ConnectionFactory
            {
                Uri = new Uri(options.Value.ConnectionString),

                // Both already default to true in RabbitMQ.Client 7.x; set explicitly because consumption silently
                // depends on them. Topology recovery is what re-declares the exchange / queue / bindings and
                // re-subscribes the consumer after a reconnect — with it off, a reconnected connection carries no
                // consumer and the process idles looking healthy, which is the failure this adapter guards against.
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = options.Value.NetworkRecoveryInterval,
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        _gate.Dispose();
    }
}

/// <summary>
/// RabbitMQ <see cref="ISendTransport"/> — publishes envelopes to a topic exchange (routing key = destination) on a
/// channel running in publisher-confirm mode, so a send completes only once the broker has durably accepted it.
/// </summary>
internal sealed partial class RabbitMqSendTransport(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
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

    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var channel = await GetChannelAsync(cancellationToken);
        var body = serializer.Serialize(envelope.Body, envelope.BodyType);

        // Caller headers first, minus the reserved namespace, then the control headers — the adapter owns wt-*. A received
        // envelope's Headers carry the wt-* keys of THAT message, so forwarding them on a re-publish would otherwise stamp
        // the previous message's type token onto the new body and misroute it on the consumer.
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in envelope.Headers)
            if (!MessageHeaders.IsReserved(key))
                headers[key] = value;

        headers[RabbitMqHeaders.EventType] = typeResolver.ToTypeToken(envelope.BodyType);
        headers[RabbitMqHeaders.ContentType] = serializer.ContentType;
        if (!string.IsNullOrEmpty(envelope.PartitionKey))
            headers[RabbitMqHeaders.PartitionKey] = envelope.PartitionKey;

        var properties = new BasicProperties
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            Persistent = envelope.Durable,
            Headers = headers,
        };

        // AMQP priority is a single byte; the envelope's hint is an unbounded int, so clamp into [0, 255] rather than
        // overflow. Clamping the top end loses no relative ordering: a queue ranks by its x-max-priority ceiling (255 max,
        // 1-5 recommended) and already treats anything above that ceiling as equal to it. Only set when the caller asked —
        // an unset priority must stay absent from the frame, not be published as 0.
        if (envelope.Priority is { } priority)
            properties.Priority = (byte)Math.Clamp(priority, byte.MinValue, byte.MaxValue);

        // RabbitMQ expects per-message TTL as a millisecond count in a string. Floor negatives to 0 — a negative
        // expiration is rejected at the channel level, while 0 is valid and means "expire unless deliverable right now".
        if (envelope.TimeToLive is { } timeToLive)
            properties.Expiration = Math.Max(0, (long)timeToLive.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

        // Returns only once the broker has acked; a nack surfaces as PublishException. Deliberately no retry around
        // this call — a republish after an ambiguous failure duplicates the message, and the broker may well have
        // accepted the first copy. The failure goes to the caller, which owns the idempotency decision.
        //
        // mandatory stays false: confirms attest that the broker accepted the message, not that a queue was bound to
        // receive it. Publishing before any consumer has declared its queue is normal here and must not throw.
        await channel.BasicPublishAsync(options.Value.Exchange, envelope.Destination, mandatory: false, properties, body, cancellationToken);
    }

    private async ValueTask<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        // The gate covers channel creation only — it is released before the publish, so awaiting confirms does not
        // serialize sends. Concurrent publishes share the channel and each awaits its own ack.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return _channel;

            // Release a channel that closed under us before replacing it, so its broker-side channel and pending
            // confirm state go away instead of waiting on a finalizer.
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
            await channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
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

/// <summary>RabbitMQ <see cref="ReceiveContext"/> — acknowledge / dead-letter via the delivering channel (nack→DLX).</summary>
internal sealed class RabbitMqReceiveContext(EventEnvelope envelope, IChannel channel, ulong deliveryTag) : ReceiveContext
{
    public override EventEnvelope Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
        => channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken);

    public override ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
        // Native DLX moves the message (RabbitMQ stamps x-death with the attempt count); reason/exception are captured
        // by the SDK dead-letter store path. Reason-header enrichment on the RabbitMQ DLQ needs a re-publish (follow-up).
        => channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken);
}

/// <summary>
/// RabbitMQ <see cref="IReceiveTransport"/> — declares topology (exchange + DLX/DLQ + queue), consumes, and routes each
/// message into the pipeline. Supervises its own consumer: a dropped connection, a closed channel, or a consumer the
/// broker cancelled is detected and the subscription re-established.
/// </summary>
internal sealed partial class RabbitMqReceiveTransport(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ILogger<RabbitMqReceiveTransport> logger) : IReceiveTransport, IAsyncDisposable
{
    /// <summary>
    /// Upper bound on how long a consumer that stopped without raising an event stays dead. Client auto-recovery can
    /// reopen the connection and channel yet leave the consumer unsubscribed — nothing signals that, so only reading
    /// the live consumer state finds it.
    /// </summary>
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Grace given to the client's own recovery before the adapter rebuilds the consumer itself. Rebuilding
    /// immediately would race topology recovery and churn a channel on every brief network blip.
    /// </summary>
    private static readonly TimeSpan RecoveryGracePeriod = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan RecoveryProbeInterval = TimeSpan.FromSeconds(1);

    private IChannel? _channel;
    private AsyncEventingBasicConsumer? _consumer;
    private IConnection? _hookedConnection;
    private volatile TaskCompletionSource _consumerLost = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _stopping;

    /// <summary>True only while a live channel carries a consumer the broker still recognises.</summary>
    private bool IsConsuming => !_stopping && _channel is { IsOpen: true } && _consumer is { IsRunning: true };

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        try
        {
            // The first establish is deliberately not retried: a broker unreachable at startup is a configuration
            // fault and still surfaces through the hosted service, as before. Only a loss after a working start
            // recovers — an outage mid-flight is not a reason to take the process down.
            await EstablishAsync(onMessage, isRecovery: false, cancellationToken);

            while (!cancellationToken.IsCancellationRequested && !_stopping)
            {
                await WaitForLossOrPollAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested || _stopping || IsConsuming)
                    continue;

                LogConsumeStopped(options.Value.Queue);

                // Let the client's automatic recovery restore the subscription first — that is the normal path and it
                // keeps the recorded consumer tag. Rebuilding is the fallback for the case it cannot cover.
                if (await WaitUntilConsumingAsync(RecoveryGracePeriod, cancellationToken))
                {
                    LogRecoveredByClient(options.Value.Queue);
                    continue;
                }

                try
                {
                    await EstablishAsync(onMessage, isRecovery: true, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Broker still unreachable. Retried on the next poll tick rather than faulting the host, which
                    // would take the whole process down over an outage the broker will recover from.
                    LogReestablishFailed(options.Value.Queue, ex);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful shutdown. The channel is left open on purpose: TransportConsumerHostedService drains handlers
            // still in flight after this returns, and they settle their deliveries on this channel before StopAsync.
        }
    }

    /// <summary>
    /// Wait until the consumer is reported lost or the poll interval elapses. Both triggers are needed: the shutdown
    /// events fire the instant a channel or connection dies, but nothing fires when recovery reopens the connection
    /// and leaves the consumer unsubscribed — the poll is the only thing that notices that.
    /// </summary>
    private async Task WaitForLossOrPollAsync(CancellationToken cancellationToken)
    {
        var lost = _consumerLost.Task;
        if (lost.IsCompleted)
            return;

        await Task.WhenAny(lost, Task.Delay(HealthPollInterval, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>Poll <see cref="IsConsuming"/> for <paramref name="window"/>; true if consumption came back on its own.</summary>
    private async Task<bool> WaitUntilConsumingAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        var probes = (int)Math.Ceiling(window / RecoveryProbeInterval);
        for (var probe = 0; probe < probes; probe++)
        {
            if (IsConsuming)
                return true;

            if (_stopping)
                return false;

            await Task.Delay(RecoveryProbeInterval, cancellationToken);
        }

        return IsConsuming;
    }

    /// <summary>Open a channel, declare the topology, and subscribe. Replaces whatever channel was there before.</summary>
    private async ValueTask EstablishAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, bool isRecovery, CancellationToken cancellationToken)
    {
        var opt = options.Value;

        // Close the previous channel before opening the replacement. Closing cancels its consumer broker-side and
        // drops it from the client's recovery list, so a channel that auto-recovery restores in parallel cannot leave
        // a second consumer on the queue competing for the same prefetch.
        await CloseChannelAsync();

        var conn = await connection.GetConnectionAsync(cancellationToken);
        HookConnection(conn);

        // Armed before anything can fail, so a death during setup signals the next wait rather than one already consumed.
        _consumerLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var channel = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
        channel.ChannelShutdownAsync += OnChannelShutdownAsync;

        // Published before the topology work so a failure part-way through cannot orphan the channel: it is already
        // the field the next attempt's CloseChannelAsync releases. IsConsuming stays false until a consumer is
        // attached below, so a half-built channel never counts as healthy.
        _channel = channel;

        await channel.ExchangeDeclareAsync(opt.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(opt.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(opt.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(opt.DeadLetterQueue, opt.DeadLetterExchange, routingKey: string.Empty, cancellationToken: cancellationToken);

        var mainArguments = new Dictionary<string, object?>(StringComparer.Ordinal) { ["x-dead-letter-exchange"] = opt.DeadLetterExchange };
        await channel.QueueDeclareAsync(opt.Queue, durable: true, exclusive: false, autoDelete: false, arguments: mainArguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(opt.Queue, opt.Exchange, routingKey: "#", cancellationToken: cancellationToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: opt.PrefetchCount, global: false, cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var envelope = TryReconstruct(delivery);
            if (envelope is null)
            {
                LogUnparseable(delivery.BasicProperties.MessageId);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                return;
            }

            await onMessage(new RabbitMqReceiveContext(envelope, channel, delivery.DeliveryTag), cancellationToken);
        };

        consumer.UnregisteredAsync += OnConsumerUnregisteredAsync;

        await channel.BasicConsumeAsync(opt.Queue, autoAck: false, consumer, cancellationToken);

        _consumer = consumer;

        if (isRecovery)
            LogConsumerReestablished(opt.Queue);
    }

    /// <summary>Detach handlers and close the current channel. Detaching first keeps our own close from reading as a fault.</summary>
    private async ValueTask CloseChannelAsync()
    {
        if (_consumer is { } consumer)
        {
            consumer.UnregisteredAsync -= OnConsumerUnregisteredAsync;
            _consumer = null;
        }

        if (_channel is { } channel)
        {
            channel.ChannelShutdownAsync -= OnChannelShutdownAsync;
            _channel = null;

            try
            {
                await channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                // Best-effort: a channel whose connection is already down cannot be closed politely.
                LogChannelDisposeFailed(ex);
            }
        }
    }

    /// <summary>Subscribe to connection-level recovery transitions once per connection instance.</summary>
    private void HookConnection(IConnection conn)
    {
        if (ReferenceEquals(_hookedConnection, conn))
            return;

        _hookedConnection = conn;
        conn.ConnectionShutdownAsync += OnConnectionShutdownAsync;
        conn.RecoverySucceededAsync += OnConnectionRecoveredAsync;
        conn.ConnectionRecoveryErrorAsync += OnConnectionRecoveryErrorAsync;
    }

    private Task OnChannelShutdownAsync(object sender, ShutdownEventArgs reason)
    {
        // ShutdownInitiator.Application means this process closed the channel — our own StopAsync/DisposeAsync, or the
        // close inside EstablishAsync. Reading that as a fault would have the supervisor rebuild a consumer the host
        // is in the middle of tearing down. A broker close is Peer; a network drop is Library. Both are real losses.
        if (_stopping || reason.Initiator == ShutdownInitiator.Application)
            return Task.CompletedTask;

        LogChannelShutdown(reason.ReplyCode, reason.ReplyText);
        _consumerLost.TrySetResult();
        return Task.CompletedTask;
    }

    private Task OnConsumerUnregisteredAsync(object sender, ConsumerEventArgs args)
    {
        if (_stopping)
            return Task.CompletedTask;

        // The broker cancelled the consumer — queue deleted, or a policy moved it. The channel stays open, so no
        // channel-shutdown event follows; without this the process would idle on a healthy-looking connection.
        LogConsumerCancelled(string.Join(", ", args.ConsumerTags));
        _consumerLost.TrySetResult();
        return Task.CompletedTask;
    }

    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs reason)
    {
        if (_stopping || reason.Initiator == ShutdownInitiator.Application)
            return Task.CompletedTask;

        LogConnectionLost(reason.ReplyCode, reason.ReplyText);
        _consumerLost.TrySetResult();
        return Task.CompletedTask;
    }

    private Task OnConnectionRecoveredAsync(object sender, AsyncEventArgs args)
    {
        LogConnectionRecovered();
        return Task.CompletedTask;
    }

    private Task OnConnectionRecoveryErrorAsync(object sender, ConnectionRecoveryErrorEventArgs args)
    {
        LogConnectionRecoveryFailed(args.Exception);
        return Task.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        // Set before disposal so the shutdown callbacks our own close raises are not mistaken for a broker fault.
        _stopping = true;
        return DisposeAsync();
    }

    private EventEnvelope? TryReconstruct(BasicDeliverEventArgs delivery)
    {
        var headers = DecodeHeaders(delivery.BasicProperties.Headers);
        if (!headers.TryGetValue(RabbitMqHeaders.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        var body = serializer.Deserialize(delivery.Body.Span, eventType);
        if (body is null)
            return null;

        return new EventEnvelope
        {
            MessageId = delivery.BasicProperties.MessageId ?? Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = delivery.RoutingKey,
            CorrelationId = delivery.BasicProperties.CorrelationId,
            DeliveryCount = delivery.Redelivered ? 2 : 1, // classic queues expose only a redelivered flag; quorum queues' x-delivery-count is a follow-up
            ContentType = headers.TryGetValue(RabbitMqHeaders.ContentType, out var contentType) ? contentType : "application/json",
            PartitionKey = headers.TryGetValue(RabbitMqHeaders.PartitionKey, out var partitionKey) && !string.IsNullOrEmpty(partitionKey) ? partitionKey : null,
            Headers = headers,
        };
    }

    private static Dictionary<string, string> DecodeHeaders(IDictionary<string, object?>? headers)
    {
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
            return decoded;

        foreach (var (key, value) in headers)
        {
            // The client stamps its own publish sequence number on every message once the send channel runs with
            // confirm tracking. It is a network-order ulong, not text, so decoding it yields mojibake — and carrying
            // it into the envelope would replay a stale sequence number on a re-publish. It is client bookkeeping,
            // not payload, so it never reaches the envelope.
            if (string.Equals(key, Constants.PublishSequenceNumberHeader, StringComparison.Ordinal))
                continue;

            decoded[key] = value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string text => text,
                _ => value?.ToString() ?? string.Empty,
            };
        }

        return decoded;
    }

    public async ValueTask DisposeAsync()
    {
        _stopping = true;

        // Wake a supervisor still parked on the loss signal so it observes _stopping and exits.
        _consumerLost.TrySetResult();

        if (_hookedConnection is { } conn)
        {
            conn.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
            conn.RecoverySucceededAsync -= OnConnectionRecoveredAsync;
            conn.ConnectionRecoveryErrorAsync -= OnConnectionRecoveryErrorAsync;
            _hookedConnection = null;
        }

        await CloseChannelAsync();
    }

    [LoggerMessage(EventId = 6301, Level = LogLevel.Warning, Message = "Discarding unparseable RabbitMQ message {MessageId}")]
    private partial void LogUnparseable(string? messageId);

    [LoggerMessage(EventId = 6303, Level = LogLevel.Warning, Message = "RabbitMQ connection lost ({ReplyCode}: {ReplyText}); awaiting automatic recovery")]
    private partial void LogConnectionLost(ushort replyCode, string? replyText);

    [LoggerMessage(EventId = 6304, Level = LogLevel.Information, Message = "RabbitMQ connection recovered")]
    private partial void LogConnectionRecovered();

    [LoggerMessage(EventId = 6305, Level = LogLevel.Error, Message = "RabbitMQ connection recovery failed; the client will retry")]
    private partial void LogConnectionRecoveryFailed(Exception exception);

    [LoggerMessage(EventId = 6306, Level = LogLevel.Warning, Message = "RabbitMQ consume channel shut down ({ReplyCode}: {ReplyText})")]
    private partial void LogChannelShutdown(ushort replyCode, string? replyText);

    [LoggerMessage(EventId = 6307, Level = LogLevel.Warning, Message = "RabbitMQ broker cancelled consumer {ConsumerTags}")]
    private partial void LogConsumerCancelled(string consumerTags);

    [LoggerMessage(EventId = 6308, Level = LogLevel.Warning, Message = "No longer consuming from RabbitMQ queue {Queue}; waiting for automatic recovery before rebuilding")]
    private partial void LogConsumeStopped(string queue);

    [LoggerMessage(EventId = 6309, Level = LogLevel.Information, Message = "Consumption on RabbitMQ queue {Queue} restored by automatic recovery")]
    private partial void LogRecoveredByClient(string queue);

    [LoggerMessage(EventId = 6310, Level = LogLevel.Information, Message = "Re-established the RabbitMQ consumer on queue {Queue}")]
    private partial void LogConsumerReestablished(string queue);

    [LoggerMessage(EventId = 6311, Level = LogLevel.Error, Message = "Re-establishing the RabbitMQ consumer on queue {Queue} failed; retrying on the next health poll")]
    private partial void LogReestablishFailed(string queue, Exception exception);

    [LoggerMessage(EventId = 6312, Level = LogLevel.Debug, Message = "Disposing the previous RabbitMQ consume channel failed; replacing it anyway")]
    private partial void LogChannelDisposeFailed(Exception exception);
}

/// <summary>
/// RabbitMQ capability flags — native dead-letter (DLX), per-message priority + TTL, publisher confirms and
/// direct-reply-to; no native delay/dedupe, no cross-queue ordering guarantee, no sessions or exactly-once transactions;
/// settlement is by delivery tag off the consume thread, so the concurrency pump may dispatch in parallel.
/// </summary>
internal sealed class RabbitMqCapabilities : ITransportCapabilities
{
    public bool NativeDeadLetter => true;

    public bool NativeDelay => false;

    public bool NativeDedupe => false;

    public bool NativeOrdering => false;

    /// <summary>
    /// Core AMQP: the <c>priority</c> message property, no plugin. Ranking is opt-in per queue — only a queue declared
    /// with the <c>x-max-priority</c> argument sorts by it; elsewhere the broker accepts the property and ignores it.
    /// The adapter's own queue declaration does not set <c>x-max-priority</c> today, so the hint currently rides the wire
    /// without taking effect (adding it to an existing queue needs a redeclare — RabbitMQ rejects an argument change on
    /// a live queue with PRECONDITION_FAILED).
    /// </summary>
    public bool NativePriority => true;

    /// <summary>Core AMQP: the per-message <c>expiration</c> property (milliseconds). Independent of any queue-level <c>x-message-ttl</c>; when both are set the lower wins.</summary>
    public bool NativeTimeToLive => true;

    /// <summary>
    /// No absolute-time enqueue. The nearest facility is the delayed-message-exchange plugin, which is (a) a plugin,
    /// not core, and (b) a relative <c>x-delay</c> in milliseconds rather than a wall-clock target.
    /// </summary>
    public bool NativeScheduling => false;

    /// <summary>
    /// No session / message-group concept. Single-active-consumer locks an entire queue to one consumer rather than a
    /// group within it, and the consistent-hash exchange (a plugin) routes by key without an exclusive group lock.
    /// </summary>
    public bool NativeSessions => false;

    /// <summary>
    /// Core AMQP publisher confirms, and the adapter uses them: the publish channel is opened in confirm mode with
    /// tracking, so a send completes only once the broker has acked it and a nack surfaces as an exception.
    /// </summary>
    public bool NativePublisherConfirms => true;

    /// <summary>
    /// AMQP <c>tx.select</c>/<c>tx.commit</c> gives atomic batch publish only — no idempotent producer, no dedupe across
    /// a retry, and no consume-transform-produce atomicity — so it does not meet the exactly-once bar this flag sets.
    /// It is also mutually exclusive with publisher confirms on a channel.
    /// </summary>
    public bool NativeTransactions => false;

    /// <summary>Core AMQP: direct-reply-to via the <c>amq.rabbitmq.reply-to</c> pseudo-queue — no reply queue to declare or clean up.</summary>
    public bool NativeRequestReply => true;

    /// <summary>RabbitMQ settles by delivery tag on the channel, which is safe from a pump worker thread.</summary>
    public bool SettlesInContext => true;

    /// <summary>Consumer callbacks are dispatched by the client, not owned by a caller-managed loop.</summary>
    public bool ThreadAffineConsume => false;
}

/// <summary>DI registration for the RabbitMQ event-bus adapter.</summary>
public static class RabbitMqServiceCollectionExtensions
{
    /// <summary>
    /// Register the RabbitMQ transport behind the SDK event bus: send/receive transports (topic exchange + native
    /// DLX/DLQ), the shared <see cref="IEventBus"/>, resilience defaults, and the consumer hosted service. Scans the
    /// supplied assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">RabbitMQ options (connection, exchange, queue, DLQ).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddRabbitMqEventBus(
        this IServiceCollection services,
        Action<RabbitMqOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<RabbitMqOptions>().Configure(configure);

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        services.TryAddSingleton<RabbitMqConnection>();
        services.TryAddSingleton<ITransportCapabilities, RabbitMqCapabilities>();
        services.TryAddSingleton<ISendTransport, RabbitMqSendTransport>();
        services.TryAddSingleton<IReceiveTransport, RabbitMqReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerHostedService>();
        return services;
    }
}
