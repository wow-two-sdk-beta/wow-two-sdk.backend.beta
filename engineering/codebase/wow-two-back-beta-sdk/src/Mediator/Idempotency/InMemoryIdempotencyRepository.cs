using Microsoft.Extensions.Caching.Memory;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Accesses process-local idempotency ownership and expiring response records.</summary>
/// <remarks>Concurrent owners receive a conflict. Ownership lasts until store or release; this is not a distributed lock.</remarks>
public sealed class InMemoryIdempotencyRepository(IMemoryCache cache) : IIdempotencyRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (Type ResponseType, Guid Ownership)> _pending = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<(bool Acquired, object? CachedResponse, Guid Ownership)> TryAcquireAsync(
        string key,
        Type responseType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(responseType);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (cache.TryGetValue((this, key), out CachedIdempotentResponse? existing))
            {
                if (existing!.ResponseType != responseType)
                {
                    throw AppErrorFactory.Conflict("The idempotency key belongs to another response contract.").ToException();
                }
                return Task.FromResult((false, existing.Response, Guid.Empty));
            }
            var ownership = Guid.NewGuid();
            if (!_pending.TryAdd(key, (responseType, ownership)))
            {
                throw AppErrorFactory.Conflict("An operation with this idempotency key is still in progress.").ToException();
            }
            return Task.FromResult<(bool, object?, Guid)>((true, null, ownership));
        }
    }

    /// <inheritdoc />
    public Task StoreAsync(string key, Guid ownership, object? response, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);
        lock (_gate)
        {
            if (!_pending.TryGetValue(key, out var slot) || slot.Ownership != ownership)
            {
                throw new InvalidOperationException("Acquire the idempotency key before storing its response.");
            }
            Type responseType = slot.ResponseType;
            if (response is not null && !responseType.IsInstanceOfType(response))
            {
                throw new InvalidOperationException("The response does not match the acquired response contract.");
            }
            cache.Set((this, key), new CachedIdempotentResponse { ResponseType = responseType, Response = response }, ttl);
            _pending.Remove(key);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(string key, Guid ownership, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_pending.TryGetValue(key, out var slot) && slot.Ownership == ownership)
            {
                _pending.Remove(key);
            }
        }
        return Task.CompletedTask;
    }
}
