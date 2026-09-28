namespace WoW.Two.Sdk.Backend.Beta.Web.RateLimit;

/// <summary>One named rate-limit policy as configuration declares it.</summary>
public sealed record RateLimitPolicySettings
{
    /// <summary>Requests (or concurrent requests, for <c>Concurrency</c>) allowed per window and partition. Default 100.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>The window, or the token-bucket replenishment period. Default 1 minute.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary><c>SlidingWindow</c> (default), <c>FixedWindow</c>, <c>TokenBucket</c> or <c>Concurrency</c>.</summary>
    public string Algorithm { get; set; } = "SlidingWindow";

    /// <summary>What a partition is: <c>Ip</c> (default), <c>User</c> (falls back to IP) or <c>Tenant</c> (falls back to IP).</summary>
    public string PartitionBy { get; set; } = "Ip";

    /// <summary>Segments of a sliding window. Default 6.</summary>
    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>Requests queued instead of rejected when the limit is reached. Default 0.</summary>
    public int QueueLimit { get; set; }
}
