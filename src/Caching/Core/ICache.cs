namespace WoW.Two.Sdk.Backend.Beta.Caching.Core;

/// <summary>
/// The SDK cache facade — a small, provider-neutral surface over a two-tier (L1 in-process + L2 distributed)
/// cache with stampede protection and tag-based invalidation. The default implementation wraps .NET's
/// <c>HybridCache</c>; call <c>AddHybridCaching()</c> to register it.
/// </summary>
public interface ICache
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or invokes <paramref name="factory"/> to create
    /// it, caches the result, and returns it. Concurrent callers for the same missing key are coalesced
    /// (stampede protection) so the factory runs once.
    /// </summary>
    /// <typeparam name="T">The cached value type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Produces the value on a miss.</param>
    /// <param name="options">Per-entry expiration/tags; <see langword="null"/> uses defaults.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The cached or newly created value.</returns>
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, overwriting any existing entry.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">Per-entry expiration/tags; <see langword="null"/> uses defaults.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Removes the entry for <paramref name="key"/> from all tiers.</summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes every entry associated with <paramref name="tag"/> from all tiers.</summary>
    /// <param name="tag">The tag whose entries should be evicted.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
