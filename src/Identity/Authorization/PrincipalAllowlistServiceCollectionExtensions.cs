using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Authorization;

/// <summary>Principal-allowlist registration.</summary>
public static class PrincipalAllowlistServiceCollectionExtensions
{
    /// <summary>Registers the claim-keyed principal allowlist — binds <see cref="AllowlistOptions"/> and adds <see cref="AllowlistAuthorizationHandler"/> as an <see cref="IAuthorizationHandler"/>; leave <see cref="AllowlistOptions.Allowed"/> empty for the OPEN single-admin default.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Populates <see cref="AllowlistOptions"/>.</param>
    public static IServiceCollection AddPrincipalAllowlist(
        this IServiceCollection services,
        Action<AllowlistOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<IAuthorizationHandler, AllowlistAuthorizationHandler>();
        return services;
    }
}
