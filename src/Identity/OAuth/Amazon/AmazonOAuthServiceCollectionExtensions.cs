using AspNet.Security.OAuth.Amazon;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Amazon;

/// <summary>Amazon OAuth provider.</summary>
public static class AmazonOAuthServiceCollectionExtensions
{
    /// <summary>Register Amazon as an OAuth provider. Pair with <c>AddCookieAuthentication</c> for the sign-in cookie.</summary>
    /// <param name="auth">The authentication builder to extend.</param>
    /// <param name="clientId">The OAuth client id.</param>
    /// <param name="clientSecret">The OAuth client secret.</param>
    /// <param name="configure">Optional tweak of the provider options.</param>
    /// <param name="scopes">Additional OAuth scopes to request.</param>
    public static AuthenticationBuilder AddAmazonAuthentication(this AuthenticationBuilder auth, string clientId, string clientSecret, Action<AmazonAuthenticationOptions>? configure = null, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        return auth.AddAmazon(o =>
        {
            o.ClientId = clientId;
            o.ClientSecret = clientSecret;
            OAuthBaseline.ApplyBaseline(o, scopes);
            configure?.Invoke(o);
            OAuthBaseline.StampProvider(o);
        });
    }
}
