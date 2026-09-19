using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Polly;

/// <summary>Holds options for the Polly-backed event resilience pipeline.</summary>
public sealed record PollyEventResilienceOptions
{
    /// <summary>Maximum retry attempts before the failure propagates (→ dead-letter). Default 5.</summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>Base delay for the exponential-with-jitter backoff. Default 200ms.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Add a circuit breaker that trips on a sustained failure ratio. Default off.</summary>
    public bool UseCircuitBreaker { get; set; }

    /// <summary>Optional per-attempt timeout. Default none.</summary>
    public TimeSpan? AttemptTimeout { get; set; }
}
