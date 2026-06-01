using Microsoft.AspNetCore.Authentication;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Microsoft;

/// <summary>Microsoft Account / Entra ID OAuth provider.</summary>
public static class MicrosoftOAuthServiceCollectionExtensions
{
    /// <summary>Register Microsoft Account as an OAuth provider.</summary>
    public static AuthenticationBuilder AddMicrosoftAuthentication(this AuthenticationBuilder auth, string clientId, string clientSecret)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        return auth.AddMicrosoftAccount(o =>
        {
            o.ClientId = clientId;
            o.ClientSecret = clientSecret;
            o.SaveTokens = true;
        });
    }
}
