using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

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

        services.AddValidatedOptions<TotpOptions>(
            configure,
            builder => builder
                .Validate(options => options.StepSeconds > 0, "TotpOptions.StepSeconds must be positive.")
                .Validate(options => options.Digits is 6 or 8, "TotpOptions.Digits must be 6 or 8.")
                .Validate(options => options.VerificationSteps >= 0, "TotpOptions.VerificationSteps must not be negative.")
                .Validate(options => options.SecretBytes > 0, "TotpOptions.SecretBytes must be positive."));
        services.TryAddSingleton<ITotpService, TotpService>();

        return services;
    }
}
