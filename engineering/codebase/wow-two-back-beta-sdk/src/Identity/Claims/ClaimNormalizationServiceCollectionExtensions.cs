using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Claim-normalization registration.</summary>
public static class ClaimNormalizationServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ClaimMapper"/> as an <see cref="IClaimsTransformation"/> so every request reads one canonical <c>wt:*</c> claim set regardless of provider. Opt-in, idempotent.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional configurator — avatar toggle, provider specs.</param>
    public static IServiceCollection AddClaimNormalization(
        this IServiceCollection services,
        Action<ClaimNormalizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ClaimNormalizationOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IClaimsTransformation, ClaimMapper>());
        return services;
    }
}
