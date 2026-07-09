using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats;

/// <summary>Options for the NATS JetStream event-bus adapter.</summary>
public sealed class NatsOptions
{
    /// <summary>NATS server URL. Default <c>nats://localhost:4222</c>.</summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>JetStream stream that persists events (and dead-letters). Created if absent. Default <c>wt-events</c>.</summary>
    public string Stream { get; set; } = "wt-events";

    /// <summary>Subject events are published to / consumed from. Default <c>wt.events</c>.</summary>
    public string Subject { get; set; } = "wt.events";

    /// <summary>Durable consumer name — survives restarts and tracks acked offsets. Default <c>wt-consumers</c>.</summary>
    public string DurableConsumer { get; set; } = "wt-consumers";

    /// <summary>Dead-letter subject — JetStream has no native DLQ, so exhausted/poison messages are re-published here (emulated DLQ). Default <c>wt.events.dlq</c>.</summary>
    public string DeadLetterSubject { get; set; } = "wt.events.dlq";

    /// <summary>Max JetStream delivery attempts before the message is abandoned by the broker (crash safety-net; in-process retries are handled by the SDK resilience pipeline). Default 5.</summary>
    public int MaxDeliver { get; set; } = 5;
}

/// <summary>Well-known NATS header keys carrying SDK envelope metadata.</summary>
internal static class NatsHeaderKeys
{
    public const string EventType = "wt-event-type";
    public const string MessageId = "wt-message-id";
}

/// <summary>Shared JetStream envelope wire-format helpers (serialize body + headers; reconstruct on receive).</summary>
internal static class NatsWireFormat
{
    public static NatsHeaders BuildHeaders(EventEnvelope envelope)
    {
        var headers = new NatsHeaders
        {
            [NatsHeaderKeys.EventType] = envelope.BodyType.AssemblyQualifiedName ?? envelope.BodyType.FullName ?? string.Empty,
            [NatsHeaderKeys.MessageId] = envelope.MessageId,
        };
        foreach (var (key, value) in envelope.Headers)
            headers[key] = value;
        return headers;
    }

    public static Dictionary<string, string> DecodeHeaders(NatsHeaders? headers)
    {
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
            return decoded;

        foreach (var key in headers.Keys)
            decoded[key] = headers[key].ToString();

        return decoded;
    }
}

/// <summary>NATS JetStream <see cref="ISendTransport"/> — publishes JSON-serialized events to the stream subject. Ensures the stream exists on first send.</summary>
internal sealed class NatsSendTransport(IOptions<NatsOptions> options) : ISendTransport, IAsyncDisposable
{
    private readonly NatsConnection _connection = new(new NatsOpts { Url = options.Value.Url });
    private readonly SemaphoreSlim _provisionGate = new(1, 1);
    private NatsJSContext? _js;
    private bool _streamReady;

    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var js = await EnsureStreamAsync(cancellationToken);
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope.Body, envelope.BodyType);
        await js.PublishAsync(options.Value.Subject, body, headers: NatsWireFormat.BuildHeaders(envelope), cancellationToken: cancellationToken);
    }

    private async ValueTask<NatsJSContext> EnsureStreamAsync(CancellationToken cancellationToken)
    {
        _js ??= new NatsJSContext(_connection);
        if (_streamReady)
            return _js;

        await _provisionGate.WaitAsync(cancellationToken);
        try
        {
            if (!_streamReady)
            {
                await NatsTopology.EnsureStreamAsync(_js, options.Value, cancellationToken);
                _streamReady = true;
            }
        }
        finally
        {
            _provisionGate.Release();
        }

        return _js;
    }

    public async ValueTask DisposeAsync()
    {
        _provisionGate.Dispose();
        await _connection.DisposeAsync();
    }
}

/// <summary>Ensures the JetStream stream (and durable consumer) exist — idempotent; ignores "already exists".</summary>
internal static class NatsTopology
{
    public static async ValueTask EnsureStreamAsync(NatsJSContext js, NatsOptions options, CancellationToken cancellationToken)
    {
        var config = new StreamConfig(options.Stream, [options.Subject, options.DeadLetterSubject]);
        try
        {
            await js.CreateStreamAsync(config, cancellationToken);
        }
        catch (NatsJSApiException)
        {
            // Stream already provisioned (name in use) — treat as ready.
        }
    }

    public static async ValueTask EnsureConsumerAsync(NatsJSContext js, NatsOptions options, CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig(options.DurableConsumer)
        {
            AckPolicy = ConsumerConfigAckPolicy.Explicit,
            MaxDeliver = options.MaxDeliver,
            FilterSubject = options.Subject, // consume the events subject only — never the dead-letter subject
        };
        await js.CreateOrUpdateConsumerAsync(options.Stream, config, cancellationToken);
    }
}

/// <summary>
/// NATS JetStream <see cref="ReceiveContext"/>. NATS.Net is thread-safe, so (unlike Kafka) the message is settled here:
/// acknowledge acks the JetStream message; dead-letter re-publishes the original to the dead-letter subject (emulated DLQ)
/// then acks so the broker stops redelivering the poison message.
/// </summary>
internal sealed class NatsReceiveContext(
    EventEnvelope envelope,
    NatsJSContext js,
    string deadLetterSubject,
    NatsJSMsg<byte[]> message) : ReceiveContext
{
    public override EventEnvelope Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
        => message.AckAsync(cancellationToken: cancellationToken);

    public override async ValueTask DeadLetterAsync(string reason, CancellationToken cancellationToken)
    {
        await js.PublishAsync(deadLetterSubject, message.Data ?? [], headers: message.Headers, cancellationToken: cancellationToken);
        await message.AckAsync(cancellationToken: cancellationToken);
    }
}

/// <summary>NATS JetStream <see cref="IReceiveTransport"/> — provisions the stream + durable consumer, then drives an async consume loop into the pipeline.</summary>
internal sealed partial class NatsReceiveTransport(IOptions<NatsOptions> options, ILogger<NatsReceiveTransport> logger) : IReceiveTransport, IAsyncDisposable
{
    private readonly NatsConnection _connection = new(new NatsOpts { Url = options.Value.Url });
    private NatsJSContext? _js;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        _js = new NatsJSContext(_connection);
        await NatsTopology.EnsureStreamAsync(_js, options.Value, cancellationToken);
        await NatsTopology.EnsureConsumerAsync(_js, options.Value, cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => ConsumeLoopAsync(onMessage, _cts.Token), CancellationToken.None);
    }

    private async Task ConsumeLoopAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        var consumer = await _js!.GetConsumerAsync(options.Value.Stream, options.Value.DurableConsumer, cancellationToken);
        try
        {
            await foreach (var message in consumer.ConsumeAsync<byte[]>(cancellationToken: cancellationToken))
            {
                var envelope = TryReconstruct(message);
                if (envelope is null)
                {
                    LogUnparseable();
                    await message.AckAsync(cancellationToken: cancellationToken); // drop — cannot route
                    continue;
                }

                var context = new NatsReceiveContext(envelope, _js!, options.Value.DeadLetterSubject, message);
                try
                {
                    // The pipeline settles via the context (ctx.Acknowledge on success / ctx.DeadLetter on exhaustion).
                    await onMessage(context, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogProcessingError(ex); // unsettled → JetStream redelivers after AckWait (deduped by the inbox)
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is { } cts)
            await cts.CancelAsync();

        if (_loop is { } loop)
        {
            try
            {
                await loop.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // stop timed out or loop cancelled — proceed to dispose
            }
        }

        await DisposeAsync();
    }

    private static EventEnvelope? TryReconstruct(NatsJSMsg<byte[]> message)
    {
        var headers = NatsWireFormat.DecodeHeaders(message.Headers);
        if (!headers.TryGetValue(NatsHeaderKeys.EventType, out var typeName) || Type.GetType(typeName) is not { } eventType)
            return null;

        if (message.Data is not { } data)
            return null;

        var body = JsonSerializer.Deserialize(data, eventType);
        if (body is null)
            return null;

        return new EventEnvelope
        {
            MessageId = headers.TryGetValue(NatsHeaderKeys.MessageId, out var id) ? id : Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = message.Subject,
            DeliveryCount = 0,
            Headers = headers,
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }

        await _connection.DisposeAsync();
    }

    [LoggerMessage(EventId = 6501, Level = LogLevel.Warning, Message = "Discarding unparseable NATS message")]
    private partial void LogUnparseable();

    [LoggerMessage(EventId = 6502, Level = LogLevel.Error, Message = "NATS message processing failed; not acknowledged (will redeliver)")]
    private partial void LogProcessingError(Exception exception);
}

/// <summary>NATS JetStream capability flags — no native DLQ (SDK emulates via a dead-letter subject); per-subject ordering.</summary>
internal sealed class NatsCapabilities : ITransportCapabilities
{
    public bool NativeDeadLetter => false;

    public bool NativeDelay => false;

    public bool NativeDedupe => false;

    public bool NativeOrdering => true;
}

/// <summary>DI registration for the NATS JetStream event-bus adapter.</summary>
public static class NatsServiceCollectionExtensions
{
    /// <summary>
    /// Register the NATS JetStream transport behind the SDK event bus: send/receive transports (stream subject +
    /// emulated dead-letter subject), the shared <see cref="IEventBus"/>, resilience defaults, and the consumer hosted
    /// service. Scans the supplied assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">NATS options (url, stream, subject, durable consumer, dead-letter subject).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddNatsEventBus(
        this IServiceCollection services,
        Action<NatsOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<NatsOptions>().Configure(configure);

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        services.TryAddSingleton<ITransportCapabilities, NatsCapabilities>();
        services.TryAddSingleton<ISendTransport, NatsSendTransport>();
        services.TryAddSingleton<IReceiveTransport, NatsReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerHostedService>();
        return services;
    }
}
