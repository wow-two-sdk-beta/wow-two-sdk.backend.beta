namespace WoW.Two.Sdk.Backend.Beta.Testing.Auth;

/// <summary>
/// The SDK's canonical <c>wt:*</c> claim types, mirrored here so the test-auth handler can stamp them without the
/// Testing package referencing the core lib (it is deliberately self-contained).
/// </summary>
/// <remarks>
/// Keep these string values in lock-step with <c>WoW.Two.Sdk.Backend.Beta.Identity.Claims.NormalizedClaimTypeConstants</c> —
/// they are the contract the SDK's <c>ClaimsPrincipalExtensions</c> read by. If the core constants change, change these.
/// </remarks>
public static class TestClaimTypeConstants
{
    /// <summary>Auth scheme that signed the user in (e.g. <c>"GitHub"</c>). Mirrors <c>NormalizedClaimTypeConstants.Provider</c>.</summary>
    public const string Provider = "wt:provider";

    /// <summary>Provider-stable user identifier. Mirrors <c>NormalizedClaimTypeConstants.UserId</c>.</summary>
    public const string UserId = "wt:user_id";

    /// <summary>Verified email address. Mirrors <c>NormalizedClaimTypeConstants.Email</c>.</summary>
    public const string Email = "wt:email";

    /// <summary>Human-friendly display name. Mirrors <c>NormalizedClaimTypeConstants.DisplayName</c>.</summary>
    public const string DisplayName = "wt:display_name";

    /// <summary>Provider handle / login. Mirrors <c>NormalizedClaimTypeConstants.Username</c>.</summary>
    public const string Username = "wt:username";

    /// <summary>Absolute URL to the user's avatar. Mirrors <c>NormalizedClaimTypeConstants.Avatar</c>.</summary>
    public const string Avatar = "wt:avatar";
}
