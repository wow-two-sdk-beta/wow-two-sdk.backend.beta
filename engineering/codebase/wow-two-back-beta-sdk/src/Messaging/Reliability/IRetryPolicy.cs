namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Defines behavior that computes the delay before the next retry attempt, or <c>null</c> when attempts are exhausted.</summary>
public interface IRetryPolicy
{
    /// <summary>Delay before <paramref name="attempt"/> (1-based, after the first failure), or <c>null</c> to stop retrying.</summary>
    /// <param name="attempt">The upcoming attempt number (1 = first retry).</param>
    /// <param name="config">The retry configuration.</param>
    TimeSpan? NextDelay(int attempt, RetryConfig config);
}
