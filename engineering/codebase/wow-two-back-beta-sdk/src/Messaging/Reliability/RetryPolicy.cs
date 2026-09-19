namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Default <see cref="IRetryPolicy"/> — fixed / exponential / exponential-with-jitter backoff, dependency-free.</summary>
public sealed class RetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public TimeSpan? NextDelay(int attempt, RetryConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (attempt < 1 || attempt >= config.MaxAttempts)
            return null;

        var baseDelay = config.BaseDelay ?? DefaultBaseDelay;
        var maxDelay = config.MaxDelay ?? DefaultMaxDelay;

        var delay = config.Backoff switch
        {
            BackoffKind.None => TimeSpan.Zero,
            BackoffKind.Fixed => baseDelay,
            BackoffKind.Exponential => ScaleExponential(baseDelay, attempt),
            BackoffKind.ExponentialJitter => ApplyJitter(ScaleExponential(baseDelay, attempt)),
            _ => baseDelay,
        };

        return delay > maxDelay ? maxDelay : delay;
    }

    private static TimeSpan ScaleExponential(TimeSpan baseDelay, int attempt)
    {
        var factor = Math.Min(Math.Pow(2, attempt - 1), 1_000_000d);
        return TimeSpan.FromTicks((long)(baseDelay.Ticks * factor));
    }

    private static TimeSpan ApplyJitter(TimeSpan delay)
    {
        var multiplier = (Random.Shared.NextDouble() * 0.5) + 0.75; // 0.75x – 1.25x
        return TimeSpan.FromTicks((long)(delay.Ticks * multiplier));
    }
}
