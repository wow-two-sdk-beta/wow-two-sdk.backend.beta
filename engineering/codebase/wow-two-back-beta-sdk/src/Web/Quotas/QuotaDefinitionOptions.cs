namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Holds one quota: its window and its limit per plan.</summary>
public sealed record QuotaDefinitionOptions
{
    /// <summary>Gets or sets the window the count resets in. Default a UTC day.</summary>
    public QuotaPeriod Period { get; set; } = QuotaPeriod.Day;

    /// <summary>Gets or sets the limit for plans not listed in <see cref="Plans"/>; a negative limit is unlimited.</summary>
    public long Limit { get; set; }

    /// <summary>Gets the limit per plan, such as <c>free: 3</c> and <c>pro: -1</c> (unlimited); names ignore case.</summary>
    public Dictionary<string, long> Plans { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The limit for <paramref name="plan"/>; null when unlimited.</summary>
    /// <param name="plan">The subject's plan.</param>
    public long? LimitFor(string? plan)
    {
        var limit = plan is not null && Plans.TryGetValue(plan, out var planned) ? planned : Limit;
        return limit < 0 ? null : limit;
    }
}
