using WoW.Two.Sdk.Backend.Beta.Caching.Core;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Invalidation;

/// <summary>Handles invalidations by evicting from the SDK cache facade.</summary>
/// <param name="cache">The HybridCache-backed cache repository.</param>
public sealed class CacheRepositoryInvalidationHandler(ICacheRepository cache) : ICacheInvalidationHandler
{
    private const string AllTag = "*";

    /// <inheritdoc />
    public ValueTask InvalidateKeyAsync(string key, CancellationToken cancellationToken) =>
        cache.RemoveAsync(key, cancellationToken);

    /// <inheritdoc />
    public ValueTask InvalidateTagAsync(string tag, CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(tag, cancellationToken);

    /// <inheritdoc />
    /// <remarks>HybridCache treats the <c>*</c> tag as every entry.</remarks>
    public ValueTask InvalidateAllAsync(CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(AllTag, cancellationToken);
}
