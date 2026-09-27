using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Invalidation.Extensions;

/// <summary>Extends EF Core writes with cache invalidations that every host receives only after commit.</summary>
public static class CacheInvalidationExtensions
{
    /// <summary>Queues an invalidation of <paramref name="key"/> on the current connection.</summary>
    /// <param name="database">The writing context's database facade.</param>
    /// <param name="key">The cache key the write makes stale.</param>
    /// <param name="channel">The notification channel every listener subscribes to.</param>
    /// <param name="cancellationToken">Cancels the notification.</param>
    /// <remarks>
    ///   - inside a transaction PostgreSQL delivers the notification only on commit; a rollback discards it
    ///   - outside a transaction it is delivered immediately
    /// </remarks>
    /// <exception cref="NotSupportedException">The provider is not PostgreSQL.</exception>
    public static Task PublishCacheKeyInvalidationAsync(
        this DatabaseFacade database,
        string key,
        string channel = CacheInvalidationConstants.DefaultChannel,
        CancellationToken cancellationToken = default) =>
        PublishAsync(database, CacheInvalidationConstants.KeyPrefix, key, channel, cancellationToken);

    /// <summary>Queues an invalidation of every entry tagged <paramref name="tag"/> on the current connection.</summary>
    /// <param name="database">The writing context's database facade.</param>
    /// <param name="tag">The cache tag the write makes stale.</param>
    /// <param name="channel">The notification channel every listener subscribes to.</param>
    /// <param name="cancellationToken">Cancels the notification.</param>
    /// <exception cref="NotSupportedException">The provider is not PostgreSQL.</exception>
    public static Task PublishCacheTagInvalidationAsync(
        this DatabaseFacade database,
        string tag,
        string channel = CacheInvalidationConstants.DefaultChannel,
        CancellationToken cancellationToken = default) =>
        PublishAsync(database, CacheInvalidationConstants.TagPrefix, tag, channel, cancellationToken);

    private static async Task PublishAsync(
        DatabaseFacade database,
        string prefix,
        string value,
        string channel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        if (!database.IsNpgsql())
        {
            throw new NotSupportedException($"Cache invalidation notifications require PostgreSQL, not '{database.ProviderName}'.");
        }

        string payload = prefix + value;
        if (Encoding.UTF8.GetByteCount(payload) > CacheInvalidationConstants.MaxPayloadBytes)
        {
            throw new ArgumentException("The invalidation payload exceeds PostgreSQL's notification limit.", nameof(value));
        }

        await database.ExecuteSqlAsync($"SELECT pg_notify({channel}, {payload})", cancellationToken).ConfigureAwait(false);
    }
}
