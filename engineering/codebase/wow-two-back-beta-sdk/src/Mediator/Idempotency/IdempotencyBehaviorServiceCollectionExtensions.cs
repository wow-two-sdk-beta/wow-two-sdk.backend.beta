using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Registration helper.</summary>
public static class IdempotencyBehaviorServiceCollectionExtensions
{
    /// <summary>Register idempotency pipeline behavior with the in-memory store (single-instance).</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorDeduplicatingInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        services.TryAddSingleton<IIdempotencyRepository, InMemoryIdempotencyRepository>();
        return services.AddMediatorInterceptor(typeof(DeduplicatingInterceptor<,>));
    }
}
