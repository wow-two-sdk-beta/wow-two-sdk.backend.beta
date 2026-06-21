using AspNet.Security.OAuth.Apple;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders.Physical;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Apple;

/// <summary>Apple Sign-In OAuth provider.</summary>
public static class AppleOAuthServiceCollectionExtensions
{
    /// <summary>Register Sign-In with Apple (services id + team id + key id + .p8 key). Pair with <c>AddCookieAuthentication</c> for the sign-in cookie.</summary>
    /// <param name="auth">Authentication builder.</param>
    /// <param name="clientId">Apple services id (client id).</param>
    /// <param name="teamId">Apple developer team id.</param>
    /// <param name="keyId">Apple sign-in key id.</param>
    /// <param name="privateKeyPath">Path to the .p8 private key file.</param>
    /// <param name="configure">Optional options hook.</param>
    /// <param name="scopes">Extra scopes to request.</param>
    public static AuthenticationBuilder AddAppleAuthentication(
        this AuthenticationBuilder auth,
        string clientId,
        string teamId,
        string keyId,
        string privateKeyPath,
        Action<AppleAuthenticationOptions>? configure = null,
        params string[] scopes)
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
            o.UsePrivateKey(_ => new PhysicalFileInfo(new FileInfo(privateKeyPath)));
            OAuthBaseline.ApplyBaseline(o, scopes);
            configure?.Invoke(o);
            OAuthBaseline.StampProvider(o);
        });
    }
}
