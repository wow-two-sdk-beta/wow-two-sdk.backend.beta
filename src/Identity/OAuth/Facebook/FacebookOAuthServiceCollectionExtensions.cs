using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Facebook;

/// <summary>Facebook OAuth provider.</summary>
public static class FacebookOAuthServiceCollectionExtensions
{
    /// <summary>Register Facebook as an OAuth provider. Pair with `AddCookieAuthentication` for the sign-in cookie.</summary>
    public static AuthenticationBuilder AddFacebookAuthentication(this AuthenticationBuilder auth, string appId, string appSecret, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(appSecret);

        return auth.AddFacebook(o =>
        {
            o.AppId = appId;
            o.AppSecret = appSecret;
            o.SaveTokens = true;
            foreach (var s in scopes) o.Scope.Add(s);
        });
    }
}
