using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Decides whether a failed delivery is re-enqueued for a later attempt, and does the re-enqueue. Consulted by the
/// consume pipeline's failure path; the resilience pipelines consult <see cref="IsActive"/> to stop their in-process
/// retry loop after one attempt so the budget is spent across deliveries rather than inside one.
/// </summary>
internal sealed partial class DelayedRetryCoordinator
{
    private readonly DelayedRetryOptions _options;
    private readonly RetryConfig _sharedRetry;
    private readonly IRetryPolicy _retryPolicy;
    private readonly IEventFaultClassifier _classifier;
    private readonly IDelayedDeliveryService? _scheduler;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DelayedRetryCoordinator> _logger;

    public DelayedRetryCoordinator(
        DelayedRetryOptions options,
        InMemoryEventBusOptions busOptions,
        IRetryPolicy retryPolicy,
        TimeProvider timeProvider,
        IEnumerable<ITransportCapabilities> capabilities,
        ILogger<DelayedRetryCoordinator> logger,
        IDelayedDeliveryService? scheduler = null,
        IEventFaultClassifier? classifier = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(busOptions);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(retryPolicy);

        _options = options;
        _sharedRetry = busOptions.Retry;
        _retryPolicy = retryPolicy;
        _timeProvider = timeProvider;
        _scheduler = scheduler;
        _logger = logger;

        // An unregistered classifier resolves to retry-everything.
        _classifier = classifier ?? DefaultEventFaultClassifier.RetryAll;

        // Resolved once at construction, so the downgrade logs once instead of on every consume.
        var transport = capabilities.FirstOrDefault();
        var canSchedule = transport is not null && (transport.NativeDelay || transport.NativeScheduling);
        IsActive = _options.Enabled && canSchedule && scheduler is not null;

        if (_options.Enabled && !IsActive)
        {
            var reason = scheduler is null
                ? "no IDelayedDeliveryService is registered"
                : "the transport reports neither NativeDelay nor NativeScheduling";
            LogInProcessDelayFallback(reason);
        }
    }

    /// <summary>
    /// True when a failed delivery is re-enqueued rather than retried in place. False leaves every retry in the
    /// in-process loop — the option is off, no scheduler is registered, or the transport cannot hold a message until a
    /// future instant.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Schedule the next attempt for a failed delivery. Returns <c>true</c> when the message has been re-published and
    /// the caller should acknowledge this delivery; <c>false</c> when it must be dead-lettered instead — the verdict is
    /// not <see cref="FaultDisposition.Retry"/>, the redelivery budget is spent, or the re-enqueue itself failed.
    /// </summary>
    /// <param name="context">The delivery that failed.</param>
    /// <param name="fault">The exception that ended it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<bool> TryReEnqueueAsync(ReceiveContext context, Exception fault, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fault);

        var scheduler = _scheduler;
        if (!IsActive || scheduler is null)
            return false;

        // Only a Retry verdict re-enqueues — a DeadLetter verdict must fail on this delivery, not a round trip later.
        if (_classifier.Classify(fault) != FaultDisposition.Retry)
            return false;

        var envelope = context.Envelope;
        var config = _options.Retry ?? _sharedRetry;
        var attempt = Math.Max(envelope.DeliveryCount, 0) + 1;

        // Cap redeliveries before consulting IRetryPolicy — a policy that never gives up would loop a poison message forever.
        if (attempt >= config.MaxAttempts)
            return false;

        var delay = _retryPolicy.NextDelay(attempt, config);
        if (delay is null)
            return false;

        // Stamp the attempt onto the wire copy — the in-process counter dies once this delivery settles.
        var notBefore = _timeProvider.GetUtcNow() + delay.Value;
        var scheduled = envelope with { DeliveryCount = attempt, NotBeforeUtc = notBefore };

        try
        {
            // Schedule before the caller acknowledges — a crash in the window redelivers the original.
            await scheduler.ScheduleAsync(scheduled, notBefore, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Return false so the original goes down the caller's dead-letter path instead of being lost.
            LogReEnqueueFailed(exception, envelope.MessageId, attempt);
            return false;
        }

        LogReEnqueued(envelope.MessageId, attempt, config.MaxAttempts, delay.Value);
        return true;
    }

    [LoggerMessage(EventId = 6051, Level = LogLevel.Warning, Message = "Delayed retry is enabled but {Reason}; retries fall back to an in-process delay that holds the consumer slot")]
    private partial void LogInProcessDelayFallback(string reason);

    [LoggerMessage(EventId = 6052, Level = LogLevel.Debug, Message = "Re-enqueued message {MessageId} for attempt {Attempt} of {MaxAttempts} in {Delay}")]
    private partial void LogReEnqueued(string messageId, int attempt, int maxAttempts, TimeSpan delay);

    [LoggerMessage(EventId = 6053, Level = LogLevel.Error, Message = "Re-enqueueing message {MessageId} for attempt {Attempt} failed; dead-lettering it instead")]
    private partial void LogReEnqueueFailed(Exception exception, string messageId, int attempt);
}
