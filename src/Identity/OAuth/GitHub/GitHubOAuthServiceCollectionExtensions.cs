using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.GitHub;

/// <summary>GitHub OAuth provider.</summary>
public static class GitHubOAuthServiceCollectionExtensions
{
    /// <summary>Register GitHub as an OAuth provider.</summary>
    public static AuthenticationBuilder AddGitHubAuthentication(this AuthenticationBuilder auth, string clientId, string clientSecret, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        return auth.AddGitHub(o =>
        {
            o.ClientId = clientId;
            o.ClientSecret = clientSecret;
            o.SaveTokens = true;
            foreach (var s in scopes) o.Scope.Add(s);
        });
    }
}
