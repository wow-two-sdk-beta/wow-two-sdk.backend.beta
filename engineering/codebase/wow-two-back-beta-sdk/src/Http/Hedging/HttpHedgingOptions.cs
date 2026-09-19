using WoW.Two.Sdk.Backend.Beta.Http.Resilience;
using WoW.Two.Sdk.Backend.Beta.Http.Resilience.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Http.Hedging;

/// <summary>Holds configuration for the SDK's standard outbound-HTTP hedging pipeline (Polly v8 via <c>Microsoft.Extensions.Http.Resilience</c>), which races a parallel attempt when the first is slow.</summary>
/// <remarks>Use for idempotent (GET-style) calls only — hedged attempts run concurrently.</remarks>
public sealed record HttpHedgingOptions
{
    /// <summary>Maximum additional (hedged) attempts after the primary one. Default 2.</summary>
    public int MaxHedgedAttempts { get; set; } = 2;

    /// <summary>Wait before launching the next hedged attempt. Default 1s.</summary>
    public TimeSpan HedgingDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Per-attempt timeout. Must be shorter than <see cref="TotalRequestTimeout"/>. Default 10s.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Overall budget for a logical request including all hedged attempts. Default 30s.</summary>
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Rolling window the per-endpoint circuit breaker samples failures over. Must be at least 2× <see cref="AttemptTimeout"/>. Default 30s.</summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Failure ratio (0–1) within the sampling window that trips the circuit. Default 0.1 (10%).</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.1;

    /// <summary>
    /// Opts an otherwise unsafe request into concurrent replay when its operation and content
    /// have an idempotency contract. The default is <see langword="null"/>.
    /// </summary>
    public Func<HttpRequestMessage, bool>? UnsafeRequestReplaySelector { get; set; }

    internal void Validate()
    {
        if (MaxHedgedAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxHedgedAttempts), "Hedged attempts cannot be negative.");
        if (HedgingDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(HedgingDelay), "Hedging delay cannot be negative.");
        HttpTimeoutValidationExtensions.ValidateTimeouts(
            AttemptTimeout,
            TotalRequestTimeout,
            CircuitBreakerSamplingDuration,
            CircuitBreakerFailureRatio);
    }
}
