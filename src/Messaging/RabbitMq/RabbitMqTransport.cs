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

    /// <summary>Consumer prefetch (unacked in flight). Default 10.</summary>
    public ushort PrefetchCount { get; set; } = 10;
}

/// <summary>Well-known RabbitMQ header keys the adapter sets.</summary>
internal static class RabbitMqHeaders
{
    /// <summary>Carries the event's stable type token so the consumer can resolve the CLR type.</summary>
    public const string EventType = "wt-event-type";

    /// <summary>Carries the serializer content type so the consumer selects the matching deserializer.</summary>
    public const string ContentType = "wt-content-type";
}

/// <summary>Lazily-opened shared RabbitMQ connection (singleton).</summary>
internal sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory { Uri = new Uri(options.Value.ConnectionString) };
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

/// <summary>RabbitMQ <see cref="ISendTransport"/> — publishes envelopes to a topic exchange (routing key = destination).</summary>
internal sealed class RabbitMqSendTransport(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver) : ISendTransport, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var channel = await GetChannelAsync(cancellationToken);
        var body = serializer.Serialize(envelope.Body, envelope.BodyType);

        var headers = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [RabbitMqHeaders.EventType] = typeResolver.ToTypeToken(envelope.BodyType),
            [RabbitMqHeaders.ContentType] = serializer.ContentType,
        };
        foreach (var (key, value) in envelope.Headers)
            headers[key] = value;

        var properties = new BasicProperties
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            Persistent = envelope.Durable,
            Headers = headers,
        };

        await channel.BasicPublishAsync(options.Value.Exchange, envelope.Destination, mandatory: false, properties, body, cancellationToken);
    }

    private async ValueTask<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return _channel;

            var conn = await connection.GetConnectionAsync(cancellationToken);
            var channel = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
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

/// <summary>RabbitMQ <see cref="IReceiveTransport"/> — declares topology (exchange + DLX/DLQ + queue), consumes, and routes each message into the pipeline.</summary>
internal sealed partial class RabbitMqReceiveTransport(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ILogger<RabbitMqReceiveTransport> logger) : IReceiveTransport, IAsyncDisposable
{
    private IChannel? _channel;

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);
        var opt = options.Value;

        var conn = await connection.GetConnectionAsync(cancellationToken);
        var channel = await conn.CreateChannelAsync(cancellationToken: cancellationToken);
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

        await channel.BasicConsumeAsync(opt.Queue, autoAck: false, consumer, cancellationToken);

        // The consumer runs via callbacks; park until the host stops.
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken) => DisposeAsync();

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
            Headers = headers,
        };
    }

    private static Dictionary<string, string> DecodeHeaders(IDictionary<string, object?>? headers)
    {
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
            return decoded;

        foreach (var (key, value) in headers)
            decoded[key] = value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string text => text,
                _ => value?.ToString() ?? string.Empty,
            };

        return decoded;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
    }

    [LoggerMessage(EventId = 6301, Level = LogLevel.Warning, Message = "Discarding unparseable RabbitMQ message {MessageId}")]
    private partial void LogUnparseable(string? messageId);
}

/// <summary>RabbitMQ capability flags — native dead-letter (DLX); no native delay/dedupe, no cross-queue ordering guarantee.</summary>
internal sealed class RabbitMqCapabilities : ITransportCapabilities
{
    public bool NativeDeadLetter => true;

    public bool NativeDelay => false;

    public bool NativeDedupe => false;

    public bool NativeOrdering => false;
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
