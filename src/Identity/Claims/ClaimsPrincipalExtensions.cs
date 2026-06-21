using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Reads the canonical <c>wt:*</c> claims stamped by <see cref="ClaimNormalizer"/>; each accessor returns <c>null</c> when the claim is absent.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>The sign-in provider (auth scheme name), or <c>null</c>.</summary>
    /// <param name="principal">Principal to read.</param>
    public static string? GetProvider(this ClaimsPrincipal principal) => Value(principal, NormalizedClaimTypes.Provider);

    /// <summary>The provider-stable user id, or <c>null</c>.</summary>
    /// <param name="principal">Principal to read.</param>
    public static string? GetUserId(this ClaimsPrincipal principal) => Value(principal, NormalizedClaimTypes.UserId);

    /// <summary>The email address, or <c>null</c>.</summary>
    /// <param name="principal">Principal to read.</param>
    public static string? GetEmail(this ClaimsPrincipal principal) => Value(principal, NormalizedClaimTypes.Email);

    /// <summary>The display name, or <c>null</c>.</summary>
    /// <param name="principal">Principal to read.</param>
    public static string? GetDisplayName(this ClaimsPrincipal principal) => Value(principal, NormalizedClaimTypes.DisplayName);

    /// <summary>The provider handle / username, or <c>null</c>.</summary>
    /// <param name="principal">Principal to read.</param>
    public static string? GetUsername(this ClaimsPrincipal principal) => Value(principal, NormalizedClaimTypes.Username);

    /// <summary>The avatar URL, or <c>null</c>.</summary>
    /// <param name="principal">Principal to read.</param>
    public static string? GetAvatar(this ClaimsPrincipal principal) => Value(principal, NormalizedClaimTypes.Avatar);

    private static string? Value(ClaimsPrincipal principal, string type)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.FindFirst(type)?.Value;
    }
}
