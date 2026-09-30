using Microsoft.AspNetCore.Authorization;

namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Builds authorization policies that admit API keys by the scopes they grant.</summary>
public static class ApiKeyAuthorizationPolicyBuilderExtensions
{
    /// <summary>Authenticates the policy with the <c>ApiKey</c> scheme and requires a key that grants <paramref name="scope"/>.</summary>
    /// <remarks>A key without the scope is forbidden; a caller without a key is challenged.</remarks>
    /// <param name="builder">The policy builder.</param>
    /// <param name="scope">The scope the key must grant.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static AuthorizationPolicyBuilder RequireApiKeyScope(this AuthorizationPolicyBuilder builder, string scope)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        return builder
            .AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.Scheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.HasApiKeyScope(scope));
    }
}
