using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Moves the retry backoff out of the consume slot: instead of sleeping in-process with the message unsettled, the
/// failed delivery is re-published with a future <see cref="EventEnvelope.NotBeforeUtc"/> and then acknowledged, so the
/// consumer is free again for the whole of the wait.
/// </summary>
/// <remarks>
/// <para>
/// Off by default (<see cref="Enabled"/> = <c>false</c>) — an existing consumer keeps the in-process
/// <c>Task.Delay</c> behaviour until it opts in with <c>AddDelayedEventRetry(...)</c>, because the two differ in
/// observable delivery semantics:
/// </para>
/// <list type="bullet">
///   <item><description>a retry arrives as a <b>fresh delivery</b>, so the whole consume filter chain re-runs (in-process retries re-run only the core);</description></item>
///   <item><description>the message is acknowledged between attempts, so a broker's own redelivery timers, prefetch accounting, and in-flight metrics see it leave and come back;</description></item>
///   <item><description>the attempt number rides on <see cref="EventEnvelope.DeliveryCount"/> rather than living in a local, so it is visible to handlers and to anything watching the envelope.</description></item>
/// </list>
/// <para>
/// The retry <i>policy</i> is unchanged — the same <see cref="RetryConfig"/> and <see cref="IRetryPolicy"/> decide how
/// many attempts and how long each wait is. Only the place the wait happens changes.
/// </para>
/// <para>
/// Requires a transport that can hold a message until a future instant (<see cref="ITransportCapabilities.NativeDelay"/>
/// or <see cref="ITransportCapabilities.NativeScheduling"/>) plus a registered <see cref="IEventScheduler"/>. Where
/// either is missing the in-process delay is used instead and the downgrade is logged once at startup, not per message.
/// </para>
/// <para>
/// A custom <see cref="IEventResiliencePipeline"/> implementation MUST stop after a single attempt while this is
/// enabled, or a message pays both loops (the in-process one and the re-enqueue one). The built-in default and
/// Polly-backed pipelines already do. The redelivery cap below holds either way.
/// </para>
/// </remarks>
public sealed class DelayedRetryOptions
{
    /// <summary>
    /// Re-enqueue a failed message with a delay instead of waiting in the consume slot. Defaults to <c>false</c> —
    /// today's in-process behaviour — so binding a configuration section that omits the key never changes delivery
    /// semantics. <c>AddDelayedEventRetry(...)</c> turns it on before applying the caller's configuration.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Retry schedule for the re-enqueue loop. <c>null</c> (default) shares the schedule the in-process pipeline uses
    /// (<see cref="InMemoryEventBusOptions.Retry"/>), so switching the wait's location does not silently change the
    /// policy. Set it to give the re-enqueue loop its own budget — the practical case being a Polly-backed pipeline,
    /// whose retry strategy is bypassed here and whose attempt count therefore has no say.
    /// </summary>
    public RetryConfig? Retry { get; set; }
}

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
    private readonly IEventScheduler? _scheduler;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DelayedRetryCoordinator> _logger;

    public DelayedRetryCoordinator(
        IOptions<DelayedRetryOptions> options,
        IOptions<InMemoryEventBusOptions> busOptions,
        IRetryPolicy retryPolicy,
        TimeProvider timeProvider,
        IEnumerable<ITransportCapabilities> capabilities,
        ILogger<DelayedRetryCoordinator> logger,
        IEventScheduler? scheduler = null,
        IEventFaultClassifier? classifier = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(busOptions);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(retryPolicy);

        _options = options.Value;
        _sharedRetry = busOptions.Value.Retry;
        _retryPolicy = retryPolicy;
        _timeProvider = timeProvider;
        _scheduler = scheduler;
        _logger = logger;

        // Same optional-classifier reasoning as the default pipeline: a hand-rolled composition that never registered
        // one still resolves, and gets retry-everything.
        _classifier = classifier ?? DefaultEventFaultClassifier.RetryAll;

        // Resolved once, at construction: the downgrade is a property of the wiring, not of any one message, so it is
        // logged here rather than on the consume path.
        var transport = capabilities.FirstOrDefault();
        var canSchedule = transport is not null && (transport.NativeDelay || transport.NativeScheduling);
        IsActive = _options.Enabled && canSchedule && scheduler is not null;

        if (_options.Enabled && !IsActive)
        {
            var reason = scheduler is null
                ? "no IEventScheduler is registered"
                : "the transport reports neither NativeDelay nor NativeScheduling";
            LogInProcessDelayFallback(reason);
        }
    }

    /// <summary>
    /// True when a failed delivery is re-enqueued rather than retried in place. False leaves every retry in the
    /// in-process loop exactly as before this existed — the option is off, no scheduler is registered, or the transport
    /// cannot hold a message until a future instant.
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

        // A DeadLetter verdict must fail on this delivery, not one round trip later; an Ignore verdict never reaches
        // here (the resilience pipeline swallows it, and the caller acknowledges) but must not be re-enqueued either.
        if (_classifier.Classify(fault) != FaultDisposition.Retry)
            return false;

        var envelope = context.Envelope;
        var config = _options.Retry ?? _sharedRetry;
        var attempt = Math.Max(envelope.DeliveryCount, 0) + 1;

        // Redelivery cap, deliberately checked before IRetryPolicy rather than trusting it. In-process, a policy that
        // never returns null merely retries a long time inside one delivery; here the same policy would re-enqueue
        // forever, so a poison message would loop for the life of the process. IRetryPolicy is a public seam, so this
        // bound cannot depend on the implementation behind it.
        if (attempt >= config.MaxAttempts)
            return false;

        var delay = _retryPolicy.NextDelay(attempt, config);
        if (delay is null)
            return false;

        // The attempt count is stamped onto the copy that goes back on the wire, because the in-process counter dies
        // the moment this delivery is settled. It only ever moves up — the next delivery reads it back and adds one —
        // so every redelivery walks the budget down to the cap above and then dead-letters.
        var notBefore = _timeProvider.GetUtcNow() + delay.Value;
        var scheduled = envelope with { DeliveryCount = attempt, NotBeforeUtc = notBefore };

        try
        {
            // Scheduled before the caller acknowledges. A crash in the window between the two redelivers the original
            // (at-least-once, the contract); acknowledging first would drop the message outright.
            await scheduler.ScheduleAsync(scheduled, notBefore, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Never acknowledge a delivery whose successor does not exist: returning false sends the original down the
            // caller's dead-letter path, so the message is parked for triage rather than lost.
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

/// <summary>DI registration for re-enqueue-with-delay retry.</summary>
public static class DelayedRetryServiceCollectionExtensions
{
    /// <summary>
    /// Retry a failed message by re-publishing it with a delay instead of waiting in the consume slot, so the backoff
    /// stops pinning a consumer (a Kafka consume loop, a RabbitMQ prefetch slot, a concurrency-pump worker).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional overrides — chiefly a retry schedule of its own; the call itself is the opt-in.</param>
    /// <remarks>
    /// Order-independent relative to the transport and resilience registrations: the transport's capabilities and the
    /// <see cref="IEventScheduler"/> are read when the coordinator is first resolved, not when this is called. Needs a
    /// transport that can defer delivery — everything else keeps the in-process delay and says so once at startup.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddInMemoryEventBus(typeof(Program).Assembly);
    ///
    /// services.AddDelayedEventRetry();                                              // shares the in-process schedule
    /// services.AddDelayedEventRetry(o => o.Retry = new RetryConfig(MaxAttempts: 8)); // or give it its own
    /// </code>
    /// </example>
    public static IServiceCollection AddDelayedEventRetry(this IServiceCollection services, Action<DelayedRetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Enabled here rather than in the type's default, so a bound configuration section that omits the key cannot
        // silently switch delivery semantics on — but can still switch this call back off.
        services.AddOptions<DelayedRetryOptions>().Configure(options =>
        {
            options.Enabled = true;
            configure?.Invoke(options);
        });

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRetryPolicy, DefaultRetryPolicy>();
        services.TryAddSingleton<DelayedRetryCoordinator>();
        return services;
    }
}
