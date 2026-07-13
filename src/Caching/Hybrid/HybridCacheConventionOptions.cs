namespace WoW.Two.Sdk.Backend.Beta.Caching.Hybrid;

/// <summary>Conventional defaults for the HybridCache registered by <see cref="HybridCachingServiceCollectionExtensions"/>.</summary>
public sealed record HybridCacheConventionOptions
{
    /// <summary>Gets the default total (L1+L2) entry lifetime. Default 5 minutes.</summary>
    public TimeSpan DefaultExpiration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets the default in-process (L1) entry lifetime; should be ≤ <see cref="DefaultExpiration"/>. Default 1 minute.</summary>
    public TimeSpan DefaultLocalCacheExpiration { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets the maximum serialized entry size in bytes; larger values are not cached. Default 1 MB.</summary>
    public long MaximumPayloadBytes { get; init; } = 1024 * 1024;

    /// <summary>Gets the maximum cache-key length in characters. Default 1024.</summary>
    public int MaximumKeyLength { get; init; } = 1024;
}
