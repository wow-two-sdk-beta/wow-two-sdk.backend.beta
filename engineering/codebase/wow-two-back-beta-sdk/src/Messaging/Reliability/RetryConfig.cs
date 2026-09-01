namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Retry schedule configuration.</summary>
/// <param name="MaxAttempts">Maximum delivery attempts before the message is dead-lettered.</param>
/// <param name="Backoff">Backoff curve between attempts.</param>
/// <param name="BaseDelay">Base delay; defaults to 200ms when null.</param>
/// <param name="MaxDelay">Delay ceiling; defaults to 30s when null.</param>
public sealed record RetryConfig(
    int MaxAttempts = 5,
    BackoffKind Backoff = BackoffKind.ExponentialJitter,
    TimeSpan? BaseDelay = null,
    TimeSpan? MaxDelay = null);
