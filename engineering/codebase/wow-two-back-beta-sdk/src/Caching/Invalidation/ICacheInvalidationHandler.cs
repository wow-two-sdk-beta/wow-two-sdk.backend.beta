namespace WoW.Two.Sdk.Backend.Beta.Caching.Invalidation;

/// <summary>Defines evicting this host's cached entries when another host commits a change.</summary>
public interface ICacheInvalidationHandler
{
    /// <summary>Evicts the entry for <paramref name="key"/>.</summary>
    /// <param name="key">The cache key a writer invalidated.</param>
    /// <param name="cancellationToken">Cancels the eviction.</param>
    ValueTask InvalidateKeyAsync(string key, CancellationToken cancellationToken);

    /// <summary>Evicts every entry tagged <paramref name="tag"/>.</summary>
    /// <param name="tag">The cache tag a writer invalidated.</param>
    /// <param name="cancellationToken">Cancels the eviction.</param>
    ValueTask InvalidateTagAsync(string tag, CancellationToken cancellationToken);

    /// <summary>Evicts everything, because notifications may have been missed while disconnected.</summary>
    /// <param name="cancellationToken">Cancels the eviction.</param>
    ValueTask InvalidateAllAsync(CancellationToken cancellationToken);
}
