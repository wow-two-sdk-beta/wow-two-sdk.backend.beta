using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google;

/// <summary>Google ID-token verifier registration (the SPA sign-in flow, distinct from the redirect <c>AddGoogleAuthentication</c>).</summary>
public static class GoogleIdTokenVerifierServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IGoogleIdTokenVerifier"/> backed by <see cref="GoogleIdTokenVerifier"/> for client-issued Google ID tokens; configure at least one accepted audience (OAuth client id).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Configures the accepted audiences (OAuth client ids).</param>
    public static IServiceCollection AddGoogleIdTokenVerifier(
        this IServiceCollection services,
        Action<GoogleIdTokenVerifierOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddSingleton<IGoogleIdTokenVerifier, GoogleIdTokenVerifier>();
        return services;
    }
}
