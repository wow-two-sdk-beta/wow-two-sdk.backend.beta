namespace WoW.Two.Sdk.Backend.Beta.Caching.Core;

/// <summary>
/// Per-entry cache tunables for <see cref="ICache"/> — total (distributed L2) expiration, a shorter
/// in-process (L1) expiration, and tags for grouped invalidation. All are optional; unset values fall
/// back to the cache's configured defaults.
/// </summary>
public sealed record CacheEntryOptions
{
    /// <summary>Gets the total lifetime of the entry across all cache tiers. <see langword="null"/> uses the default.</summary>
    public TimeSpan? Expiration { get; init; }

    /// <summary>Gets the shorter lifetime of the in-process (L1) copy. Should be ≤ <see cref="Expiration"/>. <see langword="null"/> uses the default.</summary>
    public TimeSpan? LocalCacheExpiration { get; init; }

    /// <summary>Gets tags associated with the entry, enabling group eviction via <see cref="ICache.RemoveByTagAsync"/>.</summary>
    public IReadOnlyList<string>? Tags { get; init; }
}
