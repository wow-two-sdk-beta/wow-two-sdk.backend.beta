using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Claim-normalization registration.</summary>
public static class ClaimNormalizationServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ClaimNormalizer"/> as an <see cref="IClaimsTransformation"/> so every request reads one canonical <c>wt:*</c> claim set regardless of provider. Opt-in, idempotent.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional configurator — avatar toggle, provider profiles.</param>
    public static IServiceCollection AddClaimNormalization(
        this IServiceCollection services,
        Action<ClaimNormalizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IClaimsTransformation, ClaimNormalizer>());
        return services;
    }
}
