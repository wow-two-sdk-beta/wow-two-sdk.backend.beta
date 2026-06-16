using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Marker — request opts in to idempotency dedup.</summary>
public interface IIdempotent
{
    /// <summary>Stable key derived from the request (e.g. an `Idempotency-Key` header).</summary>
    string IdempotencyKey { get; }
}

/// <summary>Storage abstraction for idempotency dedup. Implement to plug Redis / SQL / etc.</summary>
public interface IIdempotencyStore
{
    /// <summary>Try to acquire a slot for the given key. Returns the cached response if already processed.</summary>
    Task<(bool Acquired, object? CachedResponse)> TryAcquireAsync(string key, Type responseType, CancellationToken cancellationToken);

    /// <summary>Persist the response for a previously acquired key.</summary>
    Task StoreAsync(string key, object? response, TimeSpan ttl, CancellationToken cancellationToken);
}

/// <summary>Default in-memory <see cref="IIdempotencyStore"/> — single-instance only.</summary>
public sealed class InMemoryIdempotencyStore(IMemoryCache cache) : IIdempotencyStore
{
    /// <inheritdoc />
    public Task<(bool Acquired, object? CachedResponse)> TryAcquireAsync(string key, Type responseType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (cache.TryGetValue(key, out var existing))
            return Task.FromResult((false, existing));
        return Task.FromResult<(bool, object?)>((true, null));
    }

    /// <inheritdoc />
    public Task StoreAsync(string key, object? response, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cache.Set(key, response!, ttl);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Pipeline behavior — dedupes requests marked with <see cref="IIdempotent"/>.
/// First call executes the handler and caches the response; subsequent calls with the same key return the cached response.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse>(IIdempotencyStore store) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Cache TTL — defaults to 24 hours.</summary>
    public static TimeSpan Ttl { get; set; } = TimeSpan.FromHours(24);

    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (request is not IIdempotent ide)
            return await next().ConfigureAwait(false);

        var (acquired, cached) = await store.TryAcquireAsync(ide.IdempotencyKey, typeof(TResponse), cancellationToken).ConfigureAwait(false);
        if (!acquired)
            return cached is TResponse t ? t : default!;

        var response = await next().ConfigureAwait(false);
        await store.StoreAsync(ide.IdempotencyKey, response, Ttl, cancellationToken).ConfigureAwait(false);
        return response;
    }
}

/// <summary>Registration helper.</summary>
public static class IdempotencyBehaviorServiceCollectionExtensions
{
    /// <summary>Register idempotency pipeline behavior with the in-memory store (single-instance).</summary>
    public static IServiceCollection AddMediatorIdempotencyBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        return services.AddMediatorBehavior(typeof(IdempotencyBehavior<,>));
    }
}
