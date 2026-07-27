using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Http.Auth.OAuth2ClientCredentials;

/// <summary>Provides OAuth2 <c>client_credentials</c> bearer-token acquisition for an outbound HTTP client.</summary>
public static class OAuth2ClientCredentialsHttpClientBuilderExtensions
{
    /// <summary>Adds a handler that fetches, caches (per client name, refreshing <see cref="OAuth2ClientCredentialsOptions.RefreshSkew"/> before expiry), and sends a client-credentials token as <c>Authorization: Bearer</c>; requests already carrying that header pass through.</summary>
    /// <param name="builder">The HTTP client builder to extend.</param>
    /// <param name="configure">Configures the token endpoint, client credentials, and refresh behavior.</param>
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
