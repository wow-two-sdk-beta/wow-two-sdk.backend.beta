using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Holds the retry → delay → dead-letter tier model. Exhausting the in-process retry budget no longer dead-letters a message
/// outright: it is re-published on a long delay, given a fresh budget when it comes back, and only dead-lettered once
/// the whole ladder is spent.
/// </summary>
/// <remarks>
///   - use for a fault that clears in minutes or hours — a downstream outage, a rate limit
///   - needs an <see cref="IDelayedDeliveryService"/> and a transport reporting <see cref="ITransportCapabilities.NativeDelay"/> or <see cref="ITransportCapabilities.NativeScheduling"/>
///   - stays inactive when either is missing
/// </remarks>
public sealed record SecondLevelRetryOptions
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
