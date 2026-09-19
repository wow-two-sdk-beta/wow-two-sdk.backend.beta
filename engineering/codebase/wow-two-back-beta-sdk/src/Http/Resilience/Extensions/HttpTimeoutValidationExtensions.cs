namespace WoW.Two.Sdk.Backend.Beta.Http.Resilience.Extensions;

/// <summary>Extends HTTP timeout configuration with shared argument checks.</summary>
internal static class HttpTimeoutValidationExtensions
{
    internal static void ValidateTimeouts(
        TimeSpan attemptTimeout,
        TimeSpan totalRequestTimeout,
        TimeSpan circuitBreakerSamplingDuration,
        double circuitBreakerFailureRatio)
    {
        if (attemptTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(attemptTimeout), "Attempt timeout must be positive.");
        if (totalRequestTimeout <= attemptTimeout)
            throw new ArgumentOutOfRangeException(nameof(totalRequestTimeout), "Total timeout must exceed the attempt timeout.");
        if (circuitBreakerSamplingDuration < attemptTimeout + attemptTimeout)
            throw new ArgumentOutOfRangeException(nameof(circuitBreakerSamplingDuration), "Circuit-breaker sampling must be at least twice the attempt timeout.");
        if (circuitBreakerFailureRatio is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(circuitBreakerFailureRatio), "Circuit-breaker failure ratio must be greater than 0 and at most 1.");
    }
}
