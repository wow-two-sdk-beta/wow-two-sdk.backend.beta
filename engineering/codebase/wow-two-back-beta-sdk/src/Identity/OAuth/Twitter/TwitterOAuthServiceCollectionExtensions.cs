using AspNet.Security.OAuth.Twitter;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Twitter;

/// <summary>X (Twitter) OAuth 2.0 provider (PKCE) via <c>AspNet.Security.OAuth.Twitter</c>.</summary>
public static class TwitterOAuthServiceCollectionExtensions
{
    /// <summary>Default scopes when none supplied: <c>tweet.read users.read</c>.</summary>
    private static readonly string[] DefaultScopes = ["tweet.read", "users.read"];

    /// <summary>Register X (Twitter) as an OAuth 2.0 + PKCE provider; defaults to <c>tweet.read users.read</c>. Pair with <c>AddCookieAuthentication</c> for the sign-in cookie.</summary>
    /// <param name="auth">The authentication builder to extend.</param>
    /// <param name="clientId">The OAuth 2.0 client id.</param>
    /// <param name="clientSecret">The OAuth 2.0 client secret.</param>
    /// <param name="configure">Optional tweak of the provider options.</param>
    /// <param name="scopes">Additional OAuth scopes to request; defaults apply when empty.</param>
    public static AuthenticationBuilder AddTwitterAuthentication(this AuthenticationBuilder auth, string clientId, string clientSecret, Action<TwitterAuthenticationOptions>? configure = null, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        var effectiveScopes = scopes is { Length: > 0 } ? scopes : DefaultScopes;

        return auth.AddTwitter(o =>
        {
            o.ClientId = clientId;
            o.ClientSecret = clientSecret;
            OAuthBaseline.ApplyBaseline(o, effectiveScopes);
            configure?.Invoke(o);
            OAuthBaseline.StampProvider(o);
        });
    }
}
