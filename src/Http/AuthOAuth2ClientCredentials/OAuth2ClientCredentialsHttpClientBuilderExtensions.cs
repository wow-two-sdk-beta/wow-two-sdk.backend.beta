using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Http.AuthOAuth2ClientCredentials;

/// <summary>
/// Attaches OAuth2 <c>client_credentials</c> bearer-token acquisition to an outbound HTTP client.
/// </summary>
public static class OAuth2ClientCredentialsHttpClientBuilderExtensions
{
    /// <summary>
    /// Adds a handler that fetches a client-credentials token from the configured token endpoint,
    /// caches it (per client name, refreshing <see cref="OAuth2ClientCredentialsOptions.RefreshSkew"/>
    /// before expiry), and sends it as <c>Authorization: Bearer</c> on every request.
    /// Requests that already carry an <c>Authorization</c> header pass through untouched.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="configure">Token endpoint, client id/secret, and optional scope.</param>
    public static IHttpClientBuilder AddOAuth2ClientCredentials(
        this IHttpClientBuilder builder,
        Action<OAuth2ClientCredentialsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(builder.Name, configure);
        builder.Services.TryAddSingleton(static sp => new OAuth2TokenCache(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetService<TimeProvider>() ?? TimeProvider.System));

        return builder.AddHttpMessageHandler(sp => new OAuth2ClientCredentialsHandler(
            builder.Name,
            sp.GetRequiredService<OAuth2TokenCache>(),
            sp.GetRequiredService<IOptionsMonitor<OAuth2ClientCredentialsOptions>>()));
    }
}
