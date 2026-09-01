using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Storage abstraction for idempotency dedup. Implement to plug Redis / SQL / etc.</summary>
public interface IIdempotencyRepository
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
