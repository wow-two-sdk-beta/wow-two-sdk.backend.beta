using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Reserved wire header carrying a message's position in the second-level retry ladder.</summary>
public static class SecondLevelRetryHeaders
{
    /// <summary>Reserved. How many second-level tiers this message has already been promoted through; absent means none.</summary>
    public const string Tier = MessageHeaders.ReservedPrefix + "retry-tier";

    /// <summary>Read the tier marker off an envelope; 0 when absent or unparseable.</summary>
    /// <param name="envelope">The envelope to inspect.</param>
    public static int ReadTier(EventEnvelope? envelope)
    {
        if (envelope?.Headers is not { Count: > 0 } headers || !headers.TryGetValue(Tier, out var raw))
            return 0;

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier) && tier > 0 ? tier : 0;
    }
}

/// <summary>
/// The retry → delay → dead-letter tier model. Exhausting the in-process retry budget no longer dead-letters a message
/// outright: it is re-published on a long delay, given a fresh budget when it comes back, and only dead-lettered once
/// the whole ladder is spent.
/// </summary>
/// <remarks>
/// <para>
/// The two levels answer different failures. First-level retry (the resilience pipeline, or
/// <see cref="DelayedRetryOptions"/>) covers a fault that clears in milliseconds — a deadlock, a dropped connection.
/// Second-level retry covers one that clears in minutes or hours — a downstream service that is down, a rate limit, a
/// row that another process has not written yet. Spending twenty fast attempts on the second kind burns the budget
/// before the cause has had any chance to go away.
/// </para>
/// <para>
/// Off by default (<see cref="Enabled"/> = <c>false</c>). Nothing is consulted and nothing is registered on the consume
/// path until <c>AddSecondLevelEventRetry(...)</c> is called, so an application that configures nothing dead-letters on
/// first-level exhaustion exactly as before.
/// </para>
/// <para>
/// Needs a transport that can hold a message until a future instant
/// (<see cref="ITransportCapabilities.NativeDelay"/> or <see cref="ITransportCapabilities.NativeScheduling"/>) plus a
/// registered <see cref="IEventScheduler"/> — a tier delay is measured in minutes, far too long to hold a consumer slot.
/// Where either is missing the feature stays inactive and says so once at startup rather than silently degrading into a
/// long in-process sleep.
/// </para>
/// </remarks>
public sealed class SecondLevelRetryOptions
{
    private readonly List<TimeSpan> _tiers = [];

    /// <summary>
    /// Promote a message to the next delay tier instead of dead-lettering it when the first-level budget is spent.
    /// Defaults to <c>false</c>; <c>AddSecondLevelEventRetry(...)</c> turns it on before applying the caller's
    /// configuration, so a bound configuration section that omits the key cannot switch it on by accident — but can
    /// still switch that call back off.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The delay ladder, one entry per tier, walked in order. Empty (the default) means
    /// <see cref="DefaultTiers"/> is used, so enabling the feature without naming delays still does something sensible.
    /// </summary>
    public IReadOnlyList<TimeSpan> Tiers => _tiers.Count > 0 ? _tiers : DefaultTiers;

    /// <summary>The ladder used when none is configured: 1 minute, 10 minutes, 1 hour. Three tiers spanning roughly an hour of outage.</summary>
    public static IReadOnlyList<TimeSpan> DefaultTiers { get; } =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
    ];

    /// <summary>Append one tier to the ladder.</summary>
    /// <param name="delay">How long to hold the message before its next attempt; must be positive.</param>
    public SecondLevelRetryOptions AddTier(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "A second-level retry tier must delay the message by a positive amount of time.");

        _tiers.Add(delay);
        return this;
    }

    /// <summary>Replace the ladder with the given delays, in order.</summary>
    /// <param name="delays">The tier delays; each must be positive. An empty set falls back to <see cref="DefaultTiers"/>.</param>
    public SecondLevelRetryOptions UseTiers(params TimeSpan[] delays)
    {
        ArgumentNullException.ThrowIfNull(delays);

        _tiers.Clear();
        foreach (var delay in delays)
            AddTier(delay);

        return this;
    }
}

/// <summary>
/// Decides whether a message whose first-level budget is spent moves to the next delay tier instead of the dead-letter
/// store, and performs the promotion.
/// </summary>
internal sealed partial class SecondLevelRetryCoordinator
{
    private readonly SecondLevelRetryOptions _options;
    private readonly RetryConfig _firstLevelRetry;
    private readonly IEventFaultClassifier _classifier;
    private readonly IEventScheduler? _scheduler;
    private readonly DelayedRetryCoordinator? _delayedRetry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SecondLevelRetryCoordinator> _logger;

    public SecondLevelRetryCoordinator(
        IOptions<SecondLevelRetryOptions> options,
        IOptions<InMemoryEventBusOptions> busOptions,
        IOptions<DelayedRetryOptions> delayedRetryOptions,
        TimeProvider timeProvider,
        IEnumerable<ITransportCapabilities> capabilities,
        ILogger<SecondLevelRetryCoordinator> logger,
        IEventScheduler? scheduler = null,
        IEventFaultClassifier? classifier = null,
        DelayedRetryCoordinator? delayedRetry = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(busOptions);
        ArgumentNullException.ThrowIfNull(delayedRetryOptions);
        ArgumentNullException.ThrowIfNull(capabilities);

        _options = options.Value;
        _timeProvider = timeProvider;
        _scheduler = scheduler;
        _delayedRetry = delayedRetry;
        _logger = logger;

        // Same optional-classifier reasoning as the other coordinators: a hand-rolled composition that never registered
        // one still resolves, and gets retry-everything.
        _classifier = classifier ?? DefaultEventFaultClassifier.RetryAll;

        // The first-level budget this has to wait out, resolved exactly as DelayedRetryCoordinator resolves it.
        _firstLevelRetry = delayedRetryOptions.Value.Retry ?? busOptions.Value.Retry;

        // Resolved once, at construction: whether a tier delay can be honoured is a property of the wiring, not of any
        // one message, so the downgrade is reported here rather than per message on the consume path.
        var transport = capabilities.FirstOrDefault();
        var canSchedule = transport is not null && (transport.NativeDelay || transport.NativeScheduling);
        IsActive = _options.Enabled && canSchedule && scheduler is not null;

        if (_options.Enabled && !IsActive)
        {
            var reason = scheduler is null
                ? "no IEventScheduler is registered"
                : "the transport reports neither NativeDelay nor NativeScheduling";
            LogInactive(reason);
        }
    }

    /// <summary>True when an exhausted message is promoted to a delay tier rather than dead-lettered. False leaves the dead-letter path exactly as it was before this existed.</summary>
    public bool IsActive { get; }

    /// <summary>
    /// Promote a failed delivery to its next delay tier. Returns <c>true</c> when the message has been re-published on a
    /// delay and the caller should treat this delivery as settled; <c>false</c> when it must take the dead-letter path —
    /// the feature is inactive, the fault is not retryable, the first-level budget is not spent yet, the ladder is
    /// exhausted, or the re-publish itself failed.
    /// </summary>
    /// <param name="context">The delivery that failed.</param>
    /// <param name="fault">The exception that ended it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<bool> TryPromoteAsync(ReceiveContext context, Exception fault, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fault);

        var scheduler = _scheduler;
        if (!IsActive || scheduler is null)
            return false;

        // A DeadLetter verdict must fail now, not an hour of tiers later; an Ignore verdict is swallowed by the
        // resilience pipeline and never reaches here, but must not be promoted either.
        if (_classifier.Classify(fault) != FaultDisposition.Retry)
            return false;

        var envelope = context.Envelope;

        // Under delayed retry the first-level budget is spent across deliveries rather than inside one, so an exception
        // reaching here mid-budget still has fast attempts owed to it. This mirrors DelayedRetryCoordinator's own cap
        // (attempt = DeliveryCount + 1, exhausted at MaxAttempts) so the two agree on where level one ends; without
        // delayed retry the in-process loop has already run to exhaustion by the time the exception escapes.
        if (_delayedRetry is { IsActive: true } && Math.Max(envelope.DeliveryCount, 0) + 1 < _firstLevelRetry.MaxAttempts)
            return false;

        var tier = SecondLevelRetryHeaders.ReadTier(envelope);
        var tiers = _options.Tiers;
        if (tier >= tiers.Count)
            return false; // ladder spent — the message is genuinely poison

        var delay = tiers[tier];
        var notBefore = _timeProvider.GetUtcNow() + delay;
        var headers = new Dictionary<string, string>(envelope.Headers, StringComparer.Ordinal)
        {
            [SecondLevelRetryHeaders.Tier] = (tier + 1).ToString(CultureInfo.InvariantCulture),
        };

        var promoted = envelope with
        {
            // The next tier gets a whole fresh first-level budget: the point of waiting is that the fast attempts are
            // worth trying again once the cause has had time to clear.
            DeliveryCount = 0,
            NotBeforeUtc = notBefore,
            Headers = headers,
        };

        try
        {
            // Scheduled before the caller settles. A crash in the window redelivers the original (at-least-once, the
            // contract); settling first would drop the message outright.
            await scheduler.ScheduleAsync(promoted, notBefore, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Never settle a delivery whose successor does not exist: returning false sends this one down the caller's
            // dead-letter path, so the message is parked for triage rather than lost.
            LogPromotionFailed(exception, envelope.MessageId, tier + 1);
            return false;
        }

        LogPromoted(envelope.MessageId, tier + 1, tiers.Count, delay);
        return true;
    }

    [LoggerMessage(EventId = 6061, Level = LogLevel.Warning, Message = "Second-level retry is enabled but {Reason}; an exhausted message dead-letters immediately as before")]
    private partial void LogInactive(string reason);

    [LoggerMessage(EventId = 6062, Level = LogLevel.Warning, Message = "Promoted message {MessageId} to second-level retry tier {Tier} of {TierCount}; next attempt in {Delay}")]
    private partial void LogPromoted(string messageId, int tier, int tierCount, TimeSpan delay);

    [LoggerMessage(EventId = 6063, Level = LogLevel.Error, Message = "Promoting message {MessageId} to second-level retry tier {Tier} failed; dead-lettering it instead")]
    private partial void LogPromotionFailed(Exception exception, string messageId, int tier);
}

/// <summary>
/// The consume filter that applies <see cref="SecondLevelRetryOptions"/> — catches the fault that escaped the
/// first-level retry loop and, where a tier is left, re-publishes the message on a delay and swallows the fault so the
/// pipeline settles this delivery.
/// </summary>
/// <remarks>
/// A filter rather than a pipeline change: filters wrap the resilience → dedupe → dispatch core, so an exception
/// reaching one has already exhausted the in-process retry budget — exactly the moment the tier model is defined at.
/// Registration order is filter order, so registering second-level retry after the application's own filters keeps it
/// innermost and leaves audit/wire-tap filters still seeing the fault.
/// </remarks>
internal sealed class SecondLevelRetryConsumeFilter(SecondLevelRetryCoordinator coordinator) : IConsumeFilter
{
    public async ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Inactive is the default, and it must cost nothing: no try/catch is installed at all, so the fault propagates
        // to the pipeline's dead-letter path along the identical code path it took before this filter existed.
        if (!coordinator.IsActive)
        {
            await next(context, cancellationToken);
            return;
        }

        try
        {
            await next(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (!await coordinator.TryPromoteAsync(context, exception, cancellationToken))
                throw;

            // Swallowed on purpose: the message is back on the wire with a future delivery time, so this delivery is
            // finished. The pipeline takes its success path and acknowledges — which is also why a promoted message is
            // absent from the consumed and dead-lettered counters, the same blind spot delayed retry's re-enqueue has.
        }
    }
}

/// <summary>DI registration for the second-level (retry → delay → dead-letter) tier model.</summary>
public static class SecondLevelRetryServiceCollectionExtensions
{
    /// <summary>
    /// Give an exhausted message a ladder of long delays before it is dead-lettered, so an outage that outlasts the
    /// fast retry budget does not fill the DLQ with messages that would have succeeded ten minutes later.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional tier ladder; the call itself is the opt-in. Defaults to <see cref="SecondLevelRetryOptions.DefaultTiers"/>.</param>
    /// <remarks>
    /// Order-independent relative to the transport and resilience registrations — the transport's capabilities and the
    /// <see cref="IEventScheduler"/> are read when the coordinator is first resolved, not when this is called. Order
    /// relative to <c>AddConsumeFilter&lt;T&gt;()</c> does matter: it decides where in the filter chain the promotion
    /// sits. Composes with <c>AddDelayedEventRetry()</c>, which moves the <i>first</i> level off the consume slot; the
    /// two agree on where the first-level budget ends.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddInMemoryEventBus(typeof(Program).Assembly);
    ///
    /// services.AddSecondLevelEventRetry();                                                       // 1m → 10m → 1h → DLQ
    /// services.AddSecondLevelEventRetry(o => o.UseTiers(TimeSpan.FromMinutes(5), TimeSpan.FromHours(2)));
    /// </code>
    /// </example>
    public static IServiceCollection AddSecondLevelEventRetry(this IServiceCollection services, Action<SecondLevelRetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Enabled here rather than in the type's default, so a bound configuration section that omits the key cannot
        // silently change what happens to an exhausted message.
        services.AddOptions<SecondLevelRetryOptions>().Configure(options =>
        {
            options.Enabled = true;
            configure?.Invoke(options);
        });

        // Both are read to locate the end of the first-level budget; neither is necessarily registered by the caller.
        services.AddOptions<InMemoryEventBusOptions>();
        services.AddOptions<DelayedRetryOptions>();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SecondLevelRetryCoordinator>();
        services.AddSingleton<IConsumeFilter, SecondLevelRetryConsumeFilter>();
        return services;
    }
}
