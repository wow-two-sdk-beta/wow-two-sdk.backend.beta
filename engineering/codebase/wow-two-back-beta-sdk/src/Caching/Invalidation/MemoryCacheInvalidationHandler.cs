using Microsoft.Extensions.Caching.Memory;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Invalidation;

/// <summary>Handles invalidations by evicting from the in-process <see cref="IMemoryCache"/>.</summary>
/// <remarks>
///   - a key invalidation evicts that entry only
///   - <see cref="IMemoryCache"/> has no tags, so a tag invalidation evicts everything
/// </remarks>
public sealed class MemoryCacheInvalidationHandler : ICacheInvalidationHandler
{
    private readonly MemoryCache _cache;

    /// <summary>Creates the handler over the host's memory cache.</summary>
    /// <param name="cache">The memory cache; must be the default <see cref="MemoryCache"/>, which can be cleared.</param>
    /// <exception cref="ArgumentException">The cache is another implementation.</exception>
    public MemoryCacheInvalidationHandler(IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache as MemoryCache
            ?? throw new ArgumentException("Memory cache invalidation requires the default MemoryCache implementation.", nameof(cache));
    }

    /// <inheritdoc />
    public ValueTask InvalidateKeyAsync(string key, CancellationToken cancellationToken)
    {
        _cache.Remove(key);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask InvalidateTagAsync(string tag, CancellationToken cancellationToken) =>
        InvalidateAllAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask InvalidateAllAsync(CancellationToken cancellationToken)
    {
        _cache.Clear();
        return ValueTask.CompletedTask;
    }
}
