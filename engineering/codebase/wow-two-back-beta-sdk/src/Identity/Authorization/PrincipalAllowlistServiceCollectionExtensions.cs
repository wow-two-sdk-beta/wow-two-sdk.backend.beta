using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Authorization;

/// <summary>Principal-allowlist registration.</summary>
public static class PrincipalAllowlistServiceCollectionExtensions
{
    /// <summary>Registers the claim-keyed principal allowlist — binds <see cref="AllowlistOptions"/> and adds <see cref="AllowlistAuthorizationHandler"/>; leave <see cref="AllowlistOptions.Allowed"/> empty for the OPEN single-admin default.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Populates <see cref="AllowlistOptions"/>.</param>
    public static IServiceCollection AddPrincipalAllowlist(
        this IServiceCollection services,
        Action<AllowlistOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions<AllowlistOptions>(
            configure,
            builder => builder
                .Validate(options => !string.IsNullOrWhiteSpace(options.ClaimType), "AllowlistOptions.ClaimType must not be empty.")
                .Validate(options => options.Allowed is not null, "AllowlistOptions.Allowed must not be null.")
                .Validate(options => options.Allowed is null || options.Allowed.All(value => !string.IsNullOrWhiteSpace(value)), "AllowlistOptions.Allowed must not contain empty values."));
        services.AddSingleton<IAuthorizationHandler, AllowlistAuthorizationHandler>();
        return services;
    }
}
