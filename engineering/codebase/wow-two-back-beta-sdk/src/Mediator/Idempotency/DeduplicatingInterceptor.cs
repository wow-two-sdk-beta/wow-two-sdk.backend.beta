using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Dedupes requests marked with <see cref="IIdempotent"/> — first call executes and caches, repeat keys return the cached response.</summary>
/// <param name="store">The store that tracks and caches idempotent responses.</param>
public sealed class DeduplicatingInterceptor<TRequest, TResponse>(IIdempotencyRepository store) : IRequestInterceptor<TRequest, TResponse>
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
        {
            // A cached payload of another type cannot be replayed as this response.
            return cached is TResponse replayed
                ? replayed
                : throw new InvalidOperationException(
                    $"The cached idempotent response is a '{cached?.GetType().Name ?? "null"}', not the expected '{typeof(TResponse).Name}'.");
        }

        var response = await nextStep().ConfigureAwait(false);
        await store.StoreAsync(ide.IdempotencyKey, response, Ttl, cancellationToken).ConfigureAwait(false);
        return response;
    }
}
