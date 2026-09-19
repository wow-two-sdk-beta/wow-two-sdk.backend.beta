using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Policies;

/// <summary>Role-policy registration.</summary>
public static class RolePolicyServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IRolePolicy"/> backed by a scope → allowed-roles dictionary; register your own <see cref="IRolePolicy"/> before this call to swap it.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Populates <see cref="RolePolicyOptions.Map"/>.</param>
    public static IServiceCollection AddRolePolicy(
        this IServiceCollection services,
        Action<RolePolicyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions<RolePolicyOptions>(
            configure,
            builder => builder
                .Validate(options => options.Map.Keys.All(scope => !string.IsNullOrWhiteSpace(scope)), "RolePolicyOptions.Map must not contain an empty scope.")
                .Validate(options => options.Map.Values.All(roles => roles is not null && roles.All(role => !string.IsNullOrWhiteSpace(role))), "RolePolicyOptions.Map must not contain null role sets or empty roles."));
        services.TryAddSingleton<IRolePolicy, DictionaryRolePolicy>();
        return services;
    }
}
