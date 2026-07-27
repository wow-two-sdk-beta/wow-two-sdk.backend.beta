using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Registers the tenancy resolution stack (ambient context, resolver, in-memory store).</summary>
public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ambient <see cref="ITenantContext"/> (AsyncLocal-backed singleton), the request
    /// <see cref="ITenantResolver"/>, and an in-memory <see cref="ITenantStore"/> over
    /// <see cref="TenancyConventionOptions.KnownTenants"/>. Apply the middleware with
    /// <c>UseTenantResolution()</c>; register a custom <see cref="ITenantStore"/> before this call to override the default.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Configures resolution strategies and known tenants.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddTenancy(this IServiceCollection services, Action<TenancyConventionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        services.TryAddSingleton<AmbientTenantContext>();
        services.TryAddSingleton<ITenantContext>(static sp => sp.GetRequiredService<AmbientTenantContext>());
        services.TryAddSingleton<ISettableTenantContext>(static sp => sp.GetRequiredService<AmbientTenantContext>());
        services.TryAddSingleton<ITenantResolver, RequestTenantResolver>();
        services.TryAddSingleton<ITenantStore>(static sp =>
            new InMemoryTenantStore(sp.GetRequiredService<IOptions<TenancyConventionOptions>>().Value.KnownTenants));

        return services;
    }
}
