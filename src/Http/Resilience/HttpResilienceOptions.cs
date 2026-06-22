namespace WoW.Two.Sdk.Backend.Beta.Http.Resilience;

/// <summary>Configuration for the SDK's standard outbound-HTTP resilience pipeline (retry, circuit breaker, attempt timeout, and total-request timeout) on Polly v8 via <c>Microsoft.Extensions.Http.Resilience</c>.</summary>
public sealed class HttpResilienceOptions
{
    /// <summary>Maximum retry attempts after the first try. Default 3.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Per-attempt timeout. Must be shorter than <see cref="TotalRequestTimeout"/>. Default 10s.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Overall budget for a logical request including all retries. Default 30s.</summary>
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Rolling window the circuit breaker samples failures over. Must be at least 2× <see cref="AttemptTimeout"/>. Default 30s.</summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Failure ratio (0–1) within the sampling window that trips the circuit. Default 0.1 (10%).</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.1;
}
