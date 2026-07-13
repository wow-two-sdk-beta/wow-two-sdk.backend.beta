using Microsoft.Extensions.Caching.Hybrid;
using WoW.Two.Sdk.Backend.Beta.Caching.Core;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Hybrid;

/// <summary>
/// Default <see cref="ICache"/> — adapts .NET's <see cref="HybridCache"/> (L1 in-process + optional L2
/// distributed, with built-in stampede protection and tag invalidation). Registered by
/// <see cref="HybridCachingServiceCollectionExtensions.AddHybridCaching"/>.
/// </summary>
public sealed class HybridCacheAdapter : ICache
{
    private readonly HybridCache _cache;

    /// <summary>Creates the adapter over the given HybridCache.</summary>
    /// <param name="cache">The underlying HybridCache.</param>
    public HybridCacheAdapter(HybridCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    /// <inheritdoc />
    public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        => _cache.GetOrCreateAsync(key, factory, ToEntryOptions(options), options?.Tags, cancellationToken);

    /// <inheritdoc />
    public ValueTask SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        => _cache.SetAsync(key, value, ToEntryOptions(options), options?.Tags, cancellationToken);

    /// <inheritdoc />
    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(key, cancellationToken);

    /// <inheritdoc />
    public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => _cache.RemoveByTagAsync(tag, cancellationToken);

    private static HybridCacheEntryOptions? ToEntryOptions(CacheEntryOptions? options)
        => options is null || (options.Expiration is null && options.LocalCacheExpiration is null)
            ? null
            : new HybridCacheEntryOptions
            {
                Expiration = options.Expiration,
                LocalCacheExpiration = options.LocalCacheExpiration,
            };
}
