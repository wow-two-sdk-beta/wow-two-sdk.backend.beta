using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google;

/// <summary>Google OAuth provider registration.</summary>
public static class GoogleOAuthServiceCollectionExtensions
{
    /// <summary>
    /// Register Google as an OAuth provider. Pair with `AddCookieAuthentication` for the sign-in cookie.
    /// </summary>
    public static AuthenticationBuilder AddGoogleAuthentication(this AuthenticationBuilder auth, string clientId, string clientSecret)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        return auth.AddGoogle(o =>
        {
            o.ClientId = clientId;
            o.ClientSecret = clientSecret;
            o.SaveTokens = true;
        });
    }
}
