using WoW.Two.Sdk.Backend.Beta.Caching.Core;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Hybrid;

/// <summary>Holds the defaults that steer the HybridCache registered by <see cref="HybridCachingServiceCollectionExtensions"/>.</summary>
/// <remarks>Set in code with <c>AddHybridCaching(o => …)</c> or in the host section <c>Caching:Hybrid</c>, which is applied last.</remarks>
public sealed record HybridCacheConventionOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Caching:Hybrid";

    /// <summary>Gets or sets the default total (L1+L2) entry lifetime. Default 5 minutes.</summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the default in-process (L1) entry lifetime; should be ≤ <see cref="DefaultExpiration"/>. Default 1 minute.</summary>
    public TimeSpan DefaultLocalCacheExpiration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the maximum serialized entry size in bytes; larger values are not cached. Default 1 MB.</summary>
    public long MaximumPayloadBytes { get; set; } = 1024 * 1024;

    /// <summary>Gets or sets the maximum cache-key length in characters. Default 1024.</summary>
    public int MaximumKeyLength { get; set; } = 1024;

    /// <summary>
    /// Gets or sets what a read does with an entry that no longer deserializes — typically one written before its type
    /// changed shape. Default <see cref="DeserializationFailureMode.Drop"/>: evict it and run the factory.
    /// </summary>
    public DeserializationFailureMode DeserializationFailure { get; set; } = DeserializationFailureMode.Drop;
}
