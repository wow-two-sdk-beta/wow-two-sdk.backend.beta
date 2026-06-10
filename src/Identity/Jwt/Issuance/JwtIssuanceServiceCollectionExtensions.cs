using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;

/// <summary>JWT issuance registration.</summary>
public static class JwtIssuanceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITokenIssuer"/> issuing symmetric (HMAC) JWTs. The sibling
    /// <c>AddJwtBearerAuthentication</c> handles validation — keep both on the same key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Issuer / audience / lifetime / signing key.</param>
    public static IServiceCollection AddJwtTokenIssuance(
        this IServiceCollection services,
        Action<JwtTokenIssuerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ITokenIssuer, JwtTokenIssuer>();
        return services;
    }
}
