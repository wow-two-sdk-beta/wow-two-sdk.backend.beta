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
/// Transports envelopes from RabbitMQ by declaring the topology <see cref="ITopologyService"/> describes (exchange
/// + DLX, and per endpoint a queue, its dead-letter queue, and one binding per consumed message type), consumes every
/// endpoint, and routes each message into the pipeline. Supervises its own consumers: a dropped connection, a closed
/// channel, or a consumer the broker cancelled is detected and the subscriptions re-established.
/// </summary>
internal sealed partial class RabbitMqReceiveTransport(
    RabbitMqConnection connection,
    RabbitMqOptions options,
    TopologyOptions topologyOptions,
    ITopologyService topology,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ILogger<RabbitMqReceiveTransport> logger,
    MessageSerializerRegistry? serializerRegistry = null) : IReceiveTransport, IAsyncDisposable
{
    /// <summary>The <c>#</c> binding key — every routing key on the exchange, so a queue bound with it receives every message.</summary>
    private const string CatchAllRoutingKey = "#";

    /// <summary>The endpoint queue names, for log lines. Built once: the topology is fixed for the life of the process.</summary>
    private readonly string _endpointDescription = string.Join(", ", topology.ConsumeEndpoints.Select(static endpoint => endpoint.Queue));

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

    // Replaced wholesale, never mutated — the supervisor reads it while EstablishAsync rebuilds.
    private IReadOnlyList<AsyncEventingBasicConsumer> _consumers = [];
    private IConnection? _hookedConnection;
    private volatile TaskCompletionSource _consumerLost = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _stopping;

    /// <summary>True only while a live channel carries a consumer the broker still recognises on every endpoint.</summary>
    private bool IsConsuming
    {
        get
        {
            if (_stopping || _channel is not { IsOpen: true })
                return false;

            var consumers = _consumers;
            if (consumers.Count == 0)
                return false;

            foreach (var consumer in consumers)
                if (!consumer.IsRunning)
                    return false;

            return true;
        }
    }

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        try
        {
            // No endpoints means nothing to declare or consume, so park rather than let the supervisor rebuild.
            if (topology.ConsumeEndpoints.Count == 0)
            {
                LogNoConsumeEndpoints();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return;
            }

            // Only a loss after a working start recovers; a broker unreachable at startup surfaces through the host.
            await EstablishAsync(onMessage, isRecovery: false, cancellationToken);

            while (!cancellationToken.IsCancellationRequested && !_stopping)
            {
                await WaitForLossOrPollAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested || _stopping || IsConsuming)
                    continue;

                LogConsumeStopped(_endpointDescription);

                // Let automatic recovery restore the subscription first, keeping the recorded consumer tag.
                if (await WaitUntilConsumingAsync(RecoveryGracePeriod, cancellationToken))
                {
                    LogRecoveredByClient(_endpointDescription);
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
                    // Broker still unreachable — retried on the next poll tick rather than faulting the host.
                    LogReestablishFailed(_endpointDescription, ex);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Leave the channel open — in-flight handlers settle their deliveries on it before StopAsync.
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

    /// <summary>Open a channel, declare the topology, and subscribe to every endpoint. Replaces whatever channel was there before.</summary>
    private async ValueTask EstablishAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, bool isRecovery, CancellationToken cancellationToken)
    {
        var opt = options;
        var endpoints = topology.ConsumeEndpoints;

        // Per-type endpoints each own a DLQ, so the dead-letter exchange is direct, keyed by queue name.
        var perEndpointDeadLetter = topologyOptions.Style == TopologyStyle.EndpointPerMessageType;
        var deadLetterExchangeType = perEndpointDeadLetter ? ExchangeType.Direct : ExchangeType.Fanout;

        // Close the previous channel first — that cancels its consumer broker-side and drops it from recovery.
        await CloseChannelAsync();

        var conn = await connection.GetConnectionAsync(cancellationToken);
        HookConnection(conn);

        // Runs before the consume channel exists — a rejected declare closes the channel it ran on.
        var priorityCapableQueues = await ResolvePriorityCapableQueuesAsync(conn, endpoints, perEndpointDeadLetter, cancellationToken);

        // Armed before anything can fail, so a death during setup signals the next wait rather than one already consumed.
        _consumerLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var channel = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
        channel.ChannelShutdownAsync += OnChannelShutdownAsync;

        // Publish the channel before the topology work, so the next attempt's CloseChannelAsync releases it.
        _channel = channel;

        await channel.ExchangeDeclareAsync(opt.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(opt.DeadLetterExchange, deadLetterExchangeType, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        foreach (var endpoint in endpoints)
        {
            await channel.QueueDeclareAsync(endpoint.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(
                endpoint.DeadLetterQueue,
                opt.DeadLetterExchange,
                routingKey: perEndpointDeadLetter ? endpoint.DeadLetterQueue : string.Empty,
                cancellationToken: cancellationToken);

            var arguments = QueueArguments(opt, endpoint, perEndpointDeadLetter, withPriority: priorityCapableQueues.Contains(endpoint.Queue));
            await channel.QueueDeclareAsync(endpoint.Queue, durable: true, exclusive: false, autoDelete: false, arguments: arguments, cancellationToken: cancellationToken);

            // One binding per consumed message type, so the broker filters out what this service does not handle.
            foreach (var routingKey in endpoint.RoutingKeys)
                await channel.QueueBindAsync(endpoint.Queue, opt.Exchange, routingKey, cancellationToken: cancellationToken);
        }

        // global: false makes this per-consumer, so each endpoint gets its own prefetch window.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: opt.PrefetchCount, global: false, cancellationToken: cancellationToken);

        var consumers = new List<AsyncEventingBasicConsumer>(endpoints.Count);
        foreach (var endpoint in endpoints)
        {
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

            await channel.BasicConsumeAsync(endpoint.Queue, autoAck: false, consumer, cancellationToken);
            consumers.Add(consumer);
        }

        _consumers = consumers;

        if (opt.UnbindCatchAllBinding)
            await UnbindCatchAllAsync(conn, endpoints, opt, cancellationToken);

        if (isRecovery)
            LogConsumerReestablished(_endpointDescription);
    }

    /// <summary>Queue arguments for one endpoint — where its dead letters go, and whether the broker ranks priority on it.</summary>
    private static Dictionary<string, object?> QueueArguments(RabbitMqOptions opt, EndpointTopology endpoint, bool perEndpointDeadLetter, bool withPriority)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal) { ["x-dead-letter-exchange"] = opt.DeadLetterExchange };

        // Override the routing key — a dead-lettered message otherwise keeps one that lands it in another DLQ.
        if (perEndpointDeadLetter)
            arguments["x-dead-letter-routing-key"] = endpoint.DeadLetterQueue;

        // AMQP encodes queue arguments as a field table; the ceiling has to go on the wire as a signed integer.
        if (withPriority && opt.MaxPriority is { } maxPriority)
            arguments["x-max-priority"] = (int)maxPriority;

        return arguments;
    }

    /// <summary>
    /// Decide, per endpoint queue, whether it can carry <c>x-max-priority</c>. The argument is fixed at queue
    /// creation: RabbitMQ answers a redeclare that adds it with <c>PRECONDITION_FAILED</c> and closes the channel, so
    /// a queue that predates the setting would otherwise take startup down every time. Probing on a throwaway channel
    /// keeps that rejection off the consume channel and degrades the queue to unranked priority — exactly the
    /// behaviour before the option existed — with a log line naming what to recreate.
    /// </summary>
    private async ValueTask<HashSet<string>> ResolvePriorityCapableQueuesAsync(
        IConnection conn,
        IReadOnlyList<EndpointTopology> endpoints,
        bool perEndpointDeadLetter,
        CancellationToken cancellationToken)
    {
        var capable = new HashSet<string>(StringComparer.Ordinal);
        var opt = options;
        if (opt.MaxPriority is not { } maxPriority)
            return capable;

        foreach (var endpoint in endpoints)
        {
            var arguments = QueueArguments(opt, endpoint, perEndpointDeadLetter, withPriority: true);
            if (await TryDeclareQueueAsync(conn, endpoint.Queue, arguments, cancellationToken))
                capable.Add(endpoint.Queue);
            else
                LogPriorityQueueRejected(endpoint.Queue, maxPriority);
        }

        return capable;
    }

    /// <summary>Declare a queue on a throwaway channel; false when the broker rejected the arguments as incompatible with the existing queue.</summary>
    private async ValueTask<bool> TryDeclareQueueAsync(IConnection conn, string queue, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var probe = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
        try
        {
            await probe.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: arguments, cancellationToken: cancellationToken);
            return true;
        }
        catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == Constants.PreconditionFailed)
        {
            return false;
        }
        finally
        {
            try
            {
                await probe.DisposeAsync();
            }
            catch (Exception ex)
            {
                // A rejected declare already closed this channel broker-side; disposing it politely is best-effort.
                LogProbeChannelDisposeFailed(ex);
            }
        }
    }

    /// <summary>
    /// Drop the '#' binding a pre-topology deployment still carries, so its queue stops receiving types it does not
    /// handle. On a throwaway channel: an unbind that fails must not take the consume channel down with it.
    /// </summary>
    private async ValueTask UnbindCatchAllAsync(IConnection conn, IReadOnlyList<EndpointTopology> endpoints, RabbitMqOptions opt, CancellationToken cancellationToken)
    {
        var channel = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
        try
        {
            foreach (var endpoint in endpoints)
                await channel.QueueUnbindAsync(endpoint.Queue, opt.Exchange, CatchAllRoutingKey, cancellationToken: cancellationToken);
        }
        catch (RabbitMQClientException ex)
        {
            LogCatchAllUnbindFailed(ex);
        }
        finally
        {
            try
            {
                await channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                LogProbeChannelDisposeFailed(ex);
            }
        }
    }

    /// <summary>Detach handlers and close the current channel. Detaching first keeps our own close from reading as a fault.</summary>
    private async ValueTask CloseChannelAsync()
    {
        foreach (var consumer in _consumers)
            consumer.UnregisteredAsync -= OnConsumerUnregisteredAsync;

        _consumers = [];

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
        // Application means this process closed the channel; Peer is a broker close and Library a network drop.
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

        // The broker cancelled the consumer and the channel stays open, so no shutdown event follows.
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

    private EventEnvelopeModel? TryReconstruct(BasicDeliverEventArgs delivery)
    {
        var headers = DecodeHeaders(delivery.BasicProperties.Headers);
        if (!headers.TryGetValue(MessageHeaderConstants.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        // Read the declared content type or nothing — guessing JSON would reach the wrong deserializer.
        var decoded = SerializerFor(ReadOptional(headers, MessageHeaderConstants.ContentType)).Deserialize(delivery.Body.Span, eventType);
        if (decoded.IsFailure(out _, out var body))
            return null;

        return new EventEnvelopeModel
        {
            MessageId = delivery.BasicProperties.MessageId ?? Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = delivery.RoutingKey,
            CorrelationId = delivery.BasicProperties.CorrelationId,
            DeliveryCount = delivery.Redelivered ? 2 : 1, // classic queues expose only a redelivered flag; quorum queues' x-delivery-count is a follow-up
            ContentType = headers.TryGetValue(MessageHeaderConstants.ContentType, out var contentType) ? contentType : "application/json",
            PartitionKey = headers.TryGetValue(MessageHeaderConstants.PartitionKey, out var partitionKey) && !string.IsNullOrEmpty(partitionKey) ? partitionKey : null,

            // The native property is read first; the header is the fallback for a bridged-in message.
            ReplyTo = delivery.BasicProperties.ReplyTo is { Length: > 0 } replyTo ? replyTo : ReadOptional(headers, MessageHeaderConstants.ReplyTo),
            ConversationId = ReadOptional(headers, MessageHeaderConstants.ConversationId),
            Headers = headers,
        };
    }

    private static string? ReadOptional(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>The deserializer for a received content type. Falls back to the injected serializer whenever no registry is wired, which is what keeps a single-serializer container behaving exactly as it did.</summary>
    private IMessageSerializer SerializerFor(string? contentType) => serializerRegistry?.Resolve(contentType) ?? serializer;

    private static Dictionary<string, string> DecodeHeaders(IDictionary<string, object?>? headers)
    {
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
            return decoded;

        foreach (var (key, value) in headers)
        {
            // The client's publish sequence number is network-order bookkeeping, so it never reaches the envelope.
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

    [LoggerMessage(EventId = 6308, Level = LogLevel.Warning, Message = "No longer consuming from RabbitMQ queues {Queues}; waiting for automatic recovery before rebuilding")]
    private partial void LogConsumeStopped(string queues);

    [LoggerMessage(EventId = 6309, Level = LogLevel.Information, Message = "Consumption on RabbitMQ queues {Queues} restored by automatic recovery")]
    private partial void LogRecoveredByClient(string queues);

    [LoggerMessage(EventId = 6310, Level = LogLevel.Information, Message = "Re-established the RabbitMQ consumers on queues {Queues}")]
    private partial void LogConsumerReestablished(string queues);

    [LoggerMessage(EventId = 6311, Level = LogLevel.Error, Message = "Re-establishing the RabbitMQ consumers on queues {Queues} failed; retrying on the next health poll")]
    private partial void LogReestablishFailed(string queues, Exception exception);

    [LoggerMessage(EventId = 6312, Level = LogLevel.Debug, Message = "Disposing the previous RabbitMQ consume channel failed; replacing it anyway")]
    private partial void LogChannelDisposeFailed(Exception exception);

    [LoggerMessage(EventId = 6313, Level = LogLevel.Warning, Message = "RabbitMQ queue {Queue} already exists without x-max-priority, so MaxPriority={MaxPriority} cannot apply to it; messages keep their priority property but the broker will not rank them. Recreate the queue during a drain window to enable ranking")]
    private partial void LogPriorityQueueRejected(string queue, byte maxPriority);

    [LoggerMessage(EventId = 6314, Level = LogLevel.Debug, Message = "Disposing a short-lived RabbitMQ topology channel failed; it is already closed")]
    private partial void LogProbeChannelDisposeFailed(Exception exception);

    [LoggerMessage(EventId = 6315, Level = LogLevel.Warning, Message = "Removing the '#' catch-all binding failed; the queue may still receive message types this service does not handle")]
    private partial void LogCatchAllUnbindFailed(Exception exception);

    [LoggerMessage(EventId = 6316, Level = LogLevel.Warning, Message = "No RabbitMQ consume endpoints: no IEventHandler<> is registered, so there is nothing to bind or consume")]
    private partial void LogNoConsumeEndpoints();
}
