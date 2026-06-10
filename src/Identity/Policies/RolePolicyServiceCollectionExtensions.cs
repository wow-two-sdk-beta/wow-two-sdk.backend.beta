using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Policies;

/// <summary>Role-policy registration.</summary>
public static class RolePolicyServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRolePolicy"/> backed by a scope → allowed-roles dictionary.
    /// Swap the implementation by registering your own <see cref="IRolePolicy"/> before this call.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Populates <see cref="RolePolicyOptions.Map"/>.</param>
    public static IServiceCollection AddRolePolicy(
        this IServiceCollection services,
        Action<RolePolicyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddSingleton<IRolePolicy, DictionaryRolePolicy>();
        return services;
    }
}
