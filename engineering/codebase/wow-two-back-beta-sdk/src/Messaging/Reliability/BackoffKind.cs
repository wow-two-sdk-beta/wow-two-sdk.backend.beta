namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Backoff curve applied between retry attempts.</summary>
public enum BackoffKind
{
    /// <summary>No delay — retry immediately.</summary>
    None,

    /// <summary>Constant delay equal to the base delay.</summary>
    Fixed,

    /// <summary>Exponential growth: <c>base × 2^(attempt-1)</c>, capped at the max delay.</summary>
    Exponential,

    /// <summary>Exponential growth with random jitter (recommended default for distributed work).</summary>
    ExponentialJitter,
}
