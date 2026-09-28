namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Represents a subject's standing on one quota, after a consumption or as read.</summary>
public sealed record QuotaResult
{
    /// <summary>Gets the quota name.</summary>
    public required string Quota { get; init; }

    /// <summary>Gets whether the consumption fit; always true for a read.</summary>
    public required bool Allowed { get; init; }

    /// <summary>Gets the limit in this window; null when unlimited.</summary>
    public long? Limit { get; init; }

    /// <summary>Gets the units used in this window.</summary>
    public required long Used { get; init; }

    /// <summary>Gets the units left; null when unlimited.</summary>
    public long? Remaining => Limit is { } limit ? Math.Max(limit - Used, 0) : null;

    /// <summary>Gets when the window resets; null for a quota that never resets.</summary>
    public DateTimeOffset? ResetsAt { get; init; }
}
