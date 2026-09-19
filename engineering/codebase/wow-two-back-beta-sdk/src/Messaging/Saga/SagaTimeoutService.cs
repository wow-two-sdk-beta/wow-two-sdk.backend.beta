using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// Default timeout scheduler. A timeout is an ordinary event: it is published by type, never sent to an ad-hoc
/// destination, so it lands on the routing key the saga's own handler already binds — the one addressing shape that is
/// guaranteed routable under the B4 topology.
/// </summary>
/// <remarks>
///   - delivery prefers <see cref="ITransportCapabilities.NativeDelay"/> or <see cref="ITransportCapabilities.NativeScheduling"/>, then a registered <see cref="IDelayedDeliveryService"/>, then an in-process timer
///   - the in-process timer loses pending timeouts on restart
///   - the correlation id doubles as <see cref="EventEnvelopeModel.PartitionKey"/>, so a timeout cannot race the transition that scheduled it
/// </remarks>
internal sealed partial class SagaTimeoutService : ISagaTimeoutService
{
    private readonly IEventBus _bus;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SagaTimeoutService> _logger;
    private readonly IDelayedDeliveryService? _scheduler;
    private readonly bool _transportDefers;

    public SagaTimeoutService(
        IEventBus bus,
        TimeProvider timeProvider,
        IEnumerable<ITransportCapabilities> capabilities,
        ILogger<SagaTimeoutService> logger,
        IDelayedDeliveryService? scheduler = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(capabilities);

        _bus = bus;
        _timeProvider = timeProvider;
        _logger = logger;
        _scheduler = scheduler;

        // The first registered transport's capabilities decide whether a timeout parks outside the process.
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
            [SagaHeaderConstants.TimeoutName] = request.Name,
            [SagaHeaderConstants.TimeoutToken] = request.Token,
        };

    private static EventEnvelopeModel BuildEnvelope(SagaTimeoutRequest request, DateTimeOffset due, IReadOnlyDictionary<string, string> headers)
        => new()
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Body = request.Message,
            BodyType = request.MessageType,

            // The type's own name, which the topology resolves to the routing key the saga's handler already binds.
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

    [LoggerMessage(EventId = 6118, Level = LogLevel.Warning, Message = "No transport delay and no IDelayedDeliveryService is registered; saga timeouts wait on an in-process timer and are lost on restart")]
    private partial void LogInProcessTimerFallback();

    [LoggerMessage(EventId = 6119, Level = LogLevel.Error, Message = "Publishing saga timeout {Timeout} for instance {CorrelationId} failed; the saga will not be woken")]
    private partial void LogTimeoutPublishFailed(Exception exception, string timeout, string correlationId);
}
