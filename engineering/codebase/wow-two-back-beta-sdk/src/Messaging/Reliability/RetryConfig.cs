namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Retry schedule configuration.</summary>
public sealed record RetryConfig
{
    /// <summary>Maximum delivery attempts before the message is dead-lettered.</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Backoff curve between attempts.</summary>
    public BackoffKind Backoff { get; init; } = BackoffKind.ExponentialJitter;

    /// <summary>Base delay; defaults to 200ms when null.</summary>
    public TimeSpan? BaseDelay { get; init; }

    /// <summary>Delay ceiling; defaults to 30s when null.</summary>
    public TimeSpan? MaxDelay { get; init; }
}
