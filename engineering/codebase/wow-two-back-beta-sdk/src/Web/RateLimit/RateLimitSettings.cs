namespace WoW.Two.Sdk.Backend.Beta.Web.RateLimit;

/// <summary>
/// Holds the <c>RateLimits</c> configuration section: named policies endpoints opt into with
/// <c>RequireRateLimiting(name)</c>, and an optional global policy applied to every request. Missing → nothing added.
/// </summary>
public sealed record RateLimitSettings
{
    /// <summary>The configuration section the settings bind from.</summary>
    public const string SectionName = "RateLimits";

    /// <summary>Policies by name.</summary>
    public Dictionary<string, RateLimitPolicySettings> Policies { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The policy every request passes, if any; it must name an entry of <see cref="Policies"/>.</summary>
    public string? GlobalPolicy { get; set; }
}
