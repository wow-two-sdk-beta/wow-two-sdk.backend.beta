using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.GitLab;

/// <summary>GitLab OAuth provider.</summary>
public static class GitLabOAuthServiceCollectionExtensions
{
    /// <summary>Register GitLab as an OAuth provider. Pair with `AddCookieAuthentication` for the sign-in cookie.</summary>
    public static AuthenticationBuilder AddGitLabAuthentication(this AuthenticationBuilder auth, string clientId, string clientSecret, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        return auth.AddGitLab(o =>
        {
            o.ClientId = clientId;
            o.ClientSecret = clientSecret;
            o.SaveTokens = true;
            foreach (var s in scopes) o.Scope.Add(s);
        });
    }
}
