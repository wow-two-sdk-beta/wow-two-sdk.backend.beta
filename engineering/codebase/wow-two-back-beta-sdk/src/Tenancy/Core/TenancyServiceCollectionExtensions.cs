using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

/// <summary>Registers the tenancy resolution stack (ambient context, resolver, in-memory store).</summary>
public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ambient <see cref="ITenantContext"/> (AsyncLocal-backed singleton), the request
    /// <see cref="ITenantIdService"/>, and an in-memory <see cref="ITenantRepository"/> over
    /// <see cref="TenancyConventionOptions.KnownTenants"/>. Apply the middleware with
    /// <c>UseTenantResolution()</c>; register a custom <see cref="ITenantRepository"/> before this call to override the default.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Configures resolution strategies and known tenants.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddTenancy(this IServiceCollection services, Action<TenancyConventionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions<TenancyConventionOptions>(
            configure,
            builder => builder
                .Validate(options => !options.UseHeader || !string.IsNullOrWhiteSpace(options.HeaderName), "TenancyConventionOptions.HeaderName must not be empty when header resolution is enabled.")
                .Validate(options => !options.UseRoute || !string.IsNullOrWhiteSpace(options.RouteValueKey), "TenancyConventionOptions.RouteValueKey must not be empty when route resolution is enabled.")
                .Validate(options => !options.UseClaim || !string.IsNullOrWhiteSpace(options.ClaimType), "TenancyConventionOptions.ClaimType must not be empty when claim resolution is enabled.")
                .Validate(options => !options.UseSubdomain || options.SubdomainBaseLabels > 0, "TenancyConventionOptions.SubdomainBaseLabels must be positive when subdomain resolution is enabled."));

        services.TryAddSingleton<AmbientTenantContext>();
        services.TryAddSingleton<ITenantContext>(static sp => sp.GetRequiredService<AmbientTenantContext>());
        services.TryAddSingleton<ISettableTenantContext>(static sp => sp.GetRequiredService<AmbientTenantContext>());
        services.TryAddSingleton<ITenantIdService, TenantIdService>();
        services.TryAddSingleton<ITenantRepository>(static sp =>
            new InMemoryTenantRepository(sp.GetRequiredService<TenancyConventionOptions>().KnownTenants));

        return services;
    }
}
