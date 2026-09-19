using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Accesses idempotency records in process memory.</summary>
/// <param name="cache">The backing memory cache for stored responses.</param>
public sealed class InMemoryIdempotencyRepository(IMemoryCache cache) : IIdempotencyRepository
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
