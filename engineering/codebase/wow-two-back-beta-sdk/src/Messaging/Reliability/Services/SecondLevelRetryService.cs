using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Policies;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Services;

/// <summary>
/// Provides promotion to another delay tier when the first-level retry budget is spent,
/// deciding whether to reschedule the message or leave it for dead-lettering.
/// </summary>
internal sealed partial class SecondLevelRetryService
{
    private readonly SecondLevelRetryOptions _options;
    private readonly RetryConfig _firstLevelRetry;
    private readonly IEventFaultPolicy _faultPolicy;
    private readonly IDelayedDeliveryService? _scheduler;
    private readonly DelayedRetryService? _delayedRetry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SecondLevelRetryService> _logger;

    public SecondLevelRetryService(
        SecondLevelRetryOptions options,
        InMemoryEventBusOptions busOptions,
        DelayedRetryOptions delayedRetryOptions,
        TimeProvider timeProvider,
        IEnumerable<ITransportCapabilities> capabilities,
        ILogger<SecondLevelRetryService> logger,
        IDelayedDeliveryService? scheduler = null,
        IEventFaultPolicy? faultPolicy = null,
        DelayedRetryService? delayedRetry = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(busOptions);
        ArgumentNullException.ThrowIfNull(delayedRetryOptions);
        ArgumentNullException.ThrowIfNull(capabilities);

        _options = options;
        _timeProvider = timeProvider;
        _scheduler = scheduler;
        _delayedRetry = delayedRetry;
        _logger = logger;

        // No registered policy: retry everything.
        _faultPolicy = faultPolicy ?? EventFaultPolicy.RetryAll;

        // The first-level budget this has to wait out, resolved exactly as DelayedRetryService resolves it.
        _firstLevelRetry = delayedRetryOptions.Retry ?? busOptions.Retry;

        // Resolved once at construction: tier support is a property of the wiring, not of a message.
        var transport = capabilities.FirstOrDefault();
        var canSchedule = transport is not null && (transport.NativeDelay || transport.NativeScheduling);
        IsActive = _options.Enabled && canSchedule && scheduler is not null;

        if (_options.Enabled && !IsActive)
        {
            var reason = scheduler is null
                ? "no IDelayedDeliveryService is registered"
                : "the transport reports neither NativeDelay nor NativeScheduling";
            LogInactive(reason);
        }
    }

    /// <summary>True when an exhausted message is promoted to a delay tier rather than dead-lettered. False dead-letters it directly.</summary>
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

        // Promote a Retry verdict only — a DeadLetter verdict must fail now, not an hour of tiers later.
        if (_faultPolicy.Decide(fault) != FaultDisposition.Retry)
            return false;

        var envelope = context.Envelope;

        // Under delayed retry the first-level budget spans deliveries, so a mid-budget fault still owes fast attempts.
        if (_delayedRetry is { IsActive: true } && Math.Max(envelope.DeliveryCount, 0) + 1 < _firstLevelRetry.MaxAttempts)
            return false;

        var tier = SecondLevelRetryHeaderConstants.ReadTier(envelope);
        var tiers = _options.Tiers;
        if (tier >= tiers.Count)
            return false; // ladder spent — the message is genuinely poison

        var delay = tiers[tier];
        var notBefore = _timeProvider.GetUtcNow() + delay;
        var headers = new Dictionary<string, string>(envelope.Headers, StringComparer.Ordinal)
        {
            [SecondLevelRetryHeaderConstants.Tier] = (tier + 1).ToString(CultureInfo.InvariantCulture),
        };

        var promoted = envelope with
        {
            // The next tier gets a whole fresh first-level budget.
            DeliveryCount = 0,
            NotBeforeUtc = notBefore,
            Headers = headers,
        };

        try
        {
            // Scheduled before the caller settles — a crash in the window redelivers the original.
            await scheduler.ScheduleAsync(promoted, notBefore, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // No successor was scheduled, so false sends this delivery down the caller's dead-letter path.
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
