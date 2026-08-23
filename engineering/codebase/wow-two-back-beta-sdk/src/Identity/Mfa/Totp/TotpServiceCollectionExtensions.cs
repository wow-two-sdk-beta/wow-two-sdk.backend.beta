using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp;

/// <summary>Extends the identity domain with TOTP registration.</summary>
public static class TotpServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ITotpService"/> with the given TOTP parameters.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Configures step, digits and the verification window. Defaults follow RFC 6238.</param>
    public static IServiceCollection AddTotp(this IServiceCollection services, Action<TotpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<TotpOptions>().Configure(options => configure?.Invoke(options));
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<TotpOptions>>().Value);
        services.TryAddSingleton<ITotpService, TotpService>();

        return services;
    }
}
