using AspNet.Security.OAuth.Apple;
using Microsoft.AspNetCore.Authentication;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Apple;

/// <summary>Apple Sign-In OAuth provider.</summary>
public static class AppleOAuthServiceCollectionExtensions
{
    /// <summary>
    /// Register Sign-In with Apple. Requires the Apple developer key file path (.p8), Team Id, Client Id (services id), and Key Id.
    /// </summary>
    public static AuthenticationBuilder AddAppleAuthentication(
        this AuthenticationBuilder auth,
        string clientId,
        string teamId,
        string keyId,
        string privateKeyPath)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPath);

        return auth.AddApple(o =>
        {
            o.ClientId = clientId;
            o.TeamId = teamId;
            o.KeyId = keyId;
            o.UsePrivateKey(_ => new FileInfo(privateKeyPath));
            o.SaveTokens = true;
        });
    }
}
