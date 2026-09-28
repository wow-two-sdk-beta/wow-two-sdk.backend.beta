using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Caching.Core;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Hybrid;

/// <summary>
/// Accesses cached values through .NET's <see cref="HybridCache"/> (L1 in-process + optional L2
/// distributed, with built-in stampede protection and tag invalidation). Registered by
/// <see cref="HybridCachingServiceCollectionExtensions.AddHybridCaching"/>.
/// </summary>
/// <remarks>
/// An entry that no longer deserializes follows <see cref="HybridCacheConventionOptions.DeserializationFailure"/>: dropped
/// and recomputed once, or surfaced as <see cref="CacheDeserializationException"/>.
/// </remarks>
public sealed partial class HybridCacheRepository : ICacheRepository
{
    private readonly HybridCache _cache;
    private readonly IOptionsMonitor<HybridCacheConventionOptions>? _options;
    private readonly ILogger _logger;

    /// <summary>Creates the adapter over the given HybridCache.</summary>
    /// <param name="cache">The underlying HybridCache.</param>
    /// <param name="options">The conventions; <c>Caching:Hybrid</c> reloads live. Null keeps the defaults.</param>
    /// <param name="logger">Logs dropped entries.</param>
    public HybridCacheRepository(
        HybridCache cache,
        IOptionsMonitor<HybridCacheConventionOptions>? options = null,
        ILogger<HybridCacheRepository>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        _options = options;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    /// <inheritdoc />
    public async ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cache.GetOrCreateAsync(key, factory, ToEntryOptions(options), options?.Tags, cancellationToken);
        }
        catch (CacheDeserializationException exception)
        {
            if ((_options?.CurrentValue.DeserializationFailure ?? DeserializationFailureMode.Drop) == DeserializationFailureMode.Throw)
                throw new CacheDeserializationException(key, typeof(T), exception.InnerException ?? exception);

            LogEntryDropped(_logger, key, typeof(T).Name, exception.InnerException ?? exception);
            await _cache.RemoveAsync(key, cancellationToken);
            return await _cache.GetOrCreateAsync(key, factory, ToEntryOptions(options), options?.Tags, cancellationToken);
        }
    }

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

    [LoggerMessage(EventId = 9010, Level = LogLevel.Warning, Message = "Dropped cache entry {Key}: it no longer deserializes into {ValueType}; recomputing")]
    private static partial void LogEntryDropped(ILogger logger, string key, string valueType, Exception exception);
}
