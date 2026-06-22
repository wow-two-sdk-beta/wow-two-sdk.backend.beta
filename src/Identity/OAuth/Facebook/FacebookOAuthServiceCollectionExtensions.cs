using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Facebook;

/// <summary>Facebook OAuth provider.</summary>
public static class FacebookOAuthServiceCollectionExtensions
{
    /// <summary>Register Facebook as an OAuth provider. Pair with <c>AddCookieAuthentication</c> for the sign-in cookie.</summary>
    /// <param name="auth">The authentication builder to extend.</param>
    /// <param name="clientId">The Facebook app id (maps to <see cref="FacebookOptions.AppId"/>).</param>
    /// <param name="clientSecret">The Facebook app secret (maps to <see cref="FacebookOptions.AppSecret"/>).</param>
    /// <param name="configure">Optional tweak of the provider options.</param>
    /// <param name="scopes">Additional OAuth scopes to request.</param>
    public static AuthenticationBuilder AddFacebookAuthentication(this AuthenticationBuilder auth, string clientId, string clientSecret, Action<FacebookOptions>? configure = null, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        return auth.AddFacebook(o =>
        {
            o.AppId = clientId;
            o.AppSecret = clientSecret;
            OAuthBaseline.ApplyBaseline(o, scopes);
            configure?.Invoke(o);
            OAuthBaseline.StampProvider(o);
        });
    }
}
