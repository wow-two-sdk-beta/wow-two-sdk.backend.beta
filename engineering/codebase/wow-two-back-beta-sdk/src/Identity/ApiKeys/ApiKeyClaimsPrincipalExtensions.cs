using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Reads what an API key grants from the principal the <c>ApiKey</c> scheme authenticated.</summary>
public static class ApiKeyClaimsPrincipalExtensions
{
    /// <summary>Tells whether an identity the <c>ApiKey</c> scheme authenticated grants <paramref name="scope"/>.</summary>
    /// <remarks>Only the key's own identity counts, so a scope claim from another scheme never passes for a key's.</remarks>
    /// <param name="principal">The principal to read.</param>
    /// <param name="scope">The scope to look for, compared ordinally.</param>
    /// <returns><c>true</c> when the key grants the scope.</returns>
    public static bool HasApiKeyScope(this ClaimsPrincipal principal, string scope)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        return principal.Identities.Any(identity =>
            identity.IsAuthenticated
            && identity.AuthenticationType == ApiKeyAuthenticationDefaults.Scheme
            && identity.HasClaim(ApiKeyAuthenticationDefaults.ScopeClaim, scope));
    }

    /// <summary>Returns the name of the API key that authenticated the principal, or <c>null</c> for any other caller.</summary>
    /// <param name="principal">The principal to read.</param>
    /// <returns>The key's name, or <c>null</c>.</returns>
    public static string? GetApiKeyName(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.Identities
            .FirstOrDefault(identity => identity.IsAuthenticated && identity.AuthenticationType == ApiKeyAuthenticationDefaults.Scheme)
            ?.Name;
    }
}
