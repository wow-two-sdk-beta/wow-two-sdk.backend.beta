using AspNet.Security.OAuth.LinkedIn;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.LinkedIn;

/// <summary>LinkedIn OAuth provider.</summary>
public static class LinkedInOAuthServiceCollectionExtensions
{
    /// <summary>Register LinkedIn as an OAuth provider. Pair with <c>AddCookieAuthentication</c> for the sign-in cookie.</summary>
    /// <param name="auth">The authentication builder to extend.</param>
    /// <param name="clientId">The OAuth client id.</param>
    /// <param name="clientSecret">The OAuth client secret.</param>
    /// <param name="configure">Optional tweak of the provider options.</param>
    /// <param name="scopes">Additional OAuth scopes to request.</param>
    public static AuthenticationBuilder AddLinkedInAuthentication(this AuthenticationBuilder auth, string clientId, string clientSecret, Action<LinkedInAuthenticationOptions>? configure = null, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        return auth.AddLinkedIn(o =>
        {
            o.ClientId = clientId;
            o.ClientSecret = clientSecret;
            OAuthBaseline.ApplyBaseline(o, scopes);
            configure?.Invoke(o);
            OAuthBaseline.StampProvider(o);
        });
    }
}
