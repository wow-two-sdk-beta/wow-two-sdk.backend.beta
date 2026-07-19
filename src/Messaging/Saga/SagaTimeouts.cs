using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Sends a saga's timeout to itself, after the transition that asked for it has been written.</summary>
internal interface ISagaTimeoutScheduler
{
    /// <summary>Deliver <paramref name="request"/>'s message back to the saga once its delay has elapsed.</summary>
    /// <param name="request">The timeout to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ScheduleAsync(SagaTimeoutRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Default timeout scheduler. A timeout is an ordinary event: it is <b>published by type</b>, never sent to an ad-hoc
/// destination, so it lands on the routing key the saga's own handler already binds — the one addressing shape that is
/// guaranteed routable under the B4 topology.
/// </summary>
/// <remarks>
/// <para>Three delivery paths, in order of preference:</para>
/// <list type="number">
///   <item><description>the transport can hold a message until an instant (<see cref="ITransportCapabilities.NativeDelay"/> / <see cref="ITransportCapabilities.NativeScheduling"/>) — publish with a <see cref="PublishOptions.Delay"/>, which is what routes the in-memory transport's send through <see cref="IEventScheduler"/>;</description></item>
///   <item><description>it cannot, but an <see cref="IEventScheduler"/> is registered — hand the envelope to the scheduler directly, so a durable scheduler plugged in beside a broker that has no delay primitive still works;</description></item>
///   <item><description>neither — wait in-process and publish when due. Correct while the process lives, lost on restart; logged once at startup rather than per timeout.</description></item>
/// </list>
/// <para>
/// The correlation id doubles as the <see cref="EventEnvelope.PartitionKey"/>, so a timeout arrives on the same pump
/// worker as every other message for its instance and cannot race the transition that scheduled it.
/// </para>
/// </remarks>
internal sealed partial class SagaTimeoutScheduler : ISagaTimeoutScheduler
{
    private readonly IEventBus _bus;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SagaTimeoutScheduler> _logger;
    private readonly IEventScheduler? _scheduler;
    private readonly bool _transportDefers;

    public SagaTimeoutScheduler(
        IEventBus bus,
        TimeProvider timeProvider,
        IEnumerable<ITransportCapabilities> capabilities,
        ILogger<SagaTimeoutScheduler> logger,
        IEventScheduler? scheduler = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(capabilities);

        _bus = bus;
        _timeProvider = timeProvider;
        _logger = logger;
        _scheduler = scheduler;

        // Resolved once: whether a timeout can be parked outside the process is a property of the wiring, not of any
        // one timeout.
        var transport = capabilities.FirstOrDefault();
        _transportDefers = transport is not null && (transport.NativeDelay || transport.NativeScheduling);

        if (!_transportDefers && scheduler is null)
            LogInProcessTimerFallback();
    }

    public ValueTask ScheduleAsync(SagaTimeoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var due = _timeProvider.GetUtcNow() + request.Delay;
        var headers = BuildHeaders(request);

        if (_transportDefers)
        {
            return request.Publish(
                _bus,
                new PublishOptions
                {
                    CorrelationId = request.CorrelationId,
                    PartitionKey = request.CorrelationId,
                    Delay = request.Delay,
                    Headers = headers,
                },
                cancellationToken);
        }

        if (_scheduler is not null)
            return _scheduler.ScheduleAsync(BuildEnvelope(request, due, headers), due, cancellationToken);

        _ = DelayThenPublishAsync(request, headers, cancellationToken);
        return ValueTask.CompletedTask;
    }

    private static Dictionary<string, string> BuildHeaders(SagaTimeoutRequest request)
        => new(StringComparer.Ordinal)
        {
            [SagaHeaders.TimeoutName] = request.Name,
            [SagaHeaders.TimeoutToken] = request.Token,
        };

    private static EventEnvelope BuildEnvelope(SagaTimeoutRequest request, DateTimeOffset due, IReadOnlyDictionary<string, string> headers)
        => new()
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Body = request.Message,
            BodyType = request.MessageType,

            // The type's own name, which the topology resolves to the type's routing key — the binding the saga's
            // handler registration already created. An endpoint address here would be unroutable on a topic exchange.
            Destination = request.MessageType.Name,
            CorrelationId = request.CorrelationId,
            PartitionKey = request.CorrelationId,
            NotBeforeUtc = due,
            Headers = headers,
        };

    private async Task DelayThenPublishAsync(SagaTimeoutRequest request, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(request.Delay, _timeProvider, cancellationToken);
            await request.Publish(
                _bus,
                new PublishOptions
                {
                    CorrelationId = request.CorrelationId,
                    PartitionKey = request.CorrelationId,
                    Headers = headers,
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // host shutting down — the timeout dies with the process, as the startup warning said it would
        }
        catch (Exception exception)
        {
            LogTimeoutPublishFailed(exception, request.Name, request.CorrelationId);
        }
    }

    [LoggerMessage(EventId = 6118, Level = LogLevel.Warning, Message = "No transport delay and no IEventScheduler is registered; saga timeouts wait on an in-process timer and are lost on restart")]
    private partial void LogInProcessTimerFallback();

    [LoggerMessage(EventId = 6119, Level = LogLevel.Error, Message = "Publishing saga timeout {Timeout} for instance {CorrelationId} failed; the saga will not be woken")]
    private partial void LogTimeoutPublishFailed(Exception exception, string timeout, string correlationId);
}
