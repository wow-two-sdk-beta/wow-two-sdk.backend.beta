using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Microsoft;

/// <summary>Microsoft Account / Entra ID OAuth provider.</summary>
public static class MicrosoftOAuthServiceCollectionExtensions
{
    /// <summary>Register Microsoft Account as an OAuth provider. Pair with <c>AddCookieAuthentication</c> for the sign-in cookie.</summary>
    /// <param name="auth">The authentication builder.</param>
    /// <param name="clientId">The OAuth client id.</param>
    /// <param name="clientSecret">The OAuth client secret.</param>
    /// <param name="configure">Optional options hook (e.g. tenant-specific endpoints).</param>
    /// <param name="scopes">Extra scopes to request.</param>
    public static AuthenticationBuilder AddMicrosoftAuthentication(this AuthenticationBuilder auth, string clientId, string clientSecret, Action<MicrosoftAccountOptions>? configure = null, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        return auth.AddMicrosoftAccount(o =>
        {
            o.ClientId = clientId;
            o.ClientSecret = clientSecret;
            OAuthBaseline.ApplyBaseline(o, scopes);
            configure?.Invoke(o);
            OAuthBaseline.StampProvider(o);
        });
    }
}
