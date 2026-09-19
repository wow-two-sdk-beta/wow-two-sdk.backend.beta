using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google.Authenticators;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google;

/// <summary>Google ID-token authenticator registration (the SPA sign-in flow, distinct from the redirect <c>AddGoogleAuthentication</c>).</summary>
public static class GoogleIdTokenAuthenticatorServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IGoogleIdTokenAuthenticator"/> backed by <see cref="GoogleIdTokenAuthenticator"/> for client-issued Google ID tokens; configure at least one accepted audience (OAuth client id).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Configures the accepted audiences (OAuth client ids).</param>
    public static IServiceCollection AddGoogleIdTokenAuthenticator(
        this IServiceCollection services,
        Action<GoogleIdTokenAuthenticatorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions<GoogleIdTokenAuthenticatorOptions>(
            configure,
            builder => builder
                .Validate(options => options.Audiences.Count > 0, "GoogleIdTokenAuthenticatorOptions.Audiences must contain at least one client id.")
                .Validate(options => options.Audiences.All(audience => !string.IsNullOrWhiteSpace(audience)), "GoogleIdTokenAuthenticatorOptions.Audiences must not contain empty client ids."));
        services.TryAddSingleton<IGoogleIdTokenAuthenticator, GoogleIdTokenAuthenticator>();
        return services;
    }
}
