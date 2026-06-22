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
    /// <param name="key">The idempotency key to acquire.</param>
    /// <param name="responseType">The expected response type for the cached entry.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<(bool Acquired, object? CachedResponse)> TryAcquireAsync(string key, Type responseType, CancellationToken cancellationToken);

    /// <summary>Persist the response for a previously acquired key.</summary>
    /// <param name="key">The idempotency key to store under.</param>
    /// <param name="response">The response payload to cache.</param>
    /// <param name="ttl">The lifetime of the cached entry.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task StoreAsync(string key, object? response, TimeSpan ttl, CancellationToken cancellationToken);
}

/// <summary>Default in-memory <see cref="IIdempotencyStore"/> — single-instance only.</summary>
/// <param name="cache">The backing memory cache for stored responses.</param>
public sealed class InMemoryIdempotencyStore(IMemoryCache cache) : IIdempotencyStore
{
    /// <inheritdoc />
    /// <param name="key">The idempotency key to acquire.</param>
    /// <param name="responseType">The expected response type for the cached entry.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task<(bool Acquired, object? CachedResponse)> TryAcquireAsync(string key, Type responseType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (cache.TryGetValue(key, out var existing))
            return Task.FromResult((false, existing));
        return Task.FromResult<(bool, object?)>((true, null));
    }

    /// <inheritdoc />
    /// <param name="key">The idempotency key to store under.</param>
    /// <param name="response">The response payload to cache.</param>
    /// <param name="ttl">The lifetime of the cached entry.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task StoreAsync(string key, object? response, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cache.Set(key, response!, ttl);
        return Task.CompletedTask;
    }
}

/// <summary>Dedupes requests marked with <see cref="IIdempotent"/> — first call executes and caches, repeat keys return the cached response.</summary>
/// <param name="store">The store that tracks and caches idempotent responses.</param>
public sealed class IdempotencyBehavior<TRequest, TResponse>(IIdempotencyStore store) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Cache TTL — defaults to 24 hours.</summary>
    public static TimeSpan Ttl { get; set; } = TimeSpan.FromHours(24);

    /// <inheritdoc />
    /// <param name="request">The request flowing through the pipeline.</param>
    /// <param name="nextStep">The continuation that invokes the next behavior or the handler.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(nextStep);

        if (request is not IIdempotent ide)
            return await nextStep().ConfigureAwait(false);

        var (acquired, cached) = await store.TryAcquireAsync(ide.IdempotencyKey, typeof(TResponse), cancellationToken).ConfigureAwait(false);
        if (!acquired)
            return cached is TResponse t ? t : default!;

        var response = await nextStep().ConfigureAwait(false);
        await store.StoreAsync(ide.IdempotencyKey, response, Ttl, cancellationToken).ConfigureAwait(false);
        return response;
    }
}

/// <summary>Registration helper.</summary>
public static class IdempotencyBehaviorServiceCollectionExtensions
{
    /// <summary>Register idempotency pipeline behavior with the in-memory store (single-instance).</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorIdempotencyBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        return services.AddMediatorBehavior(typeof(IdempotencyBehavior<,>));
    }
}
