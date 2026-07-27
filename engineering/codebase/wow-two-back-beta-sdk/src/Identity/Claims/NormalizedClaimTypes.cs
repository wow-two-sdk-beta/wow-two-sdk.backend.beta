namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Canonical <c>wt:*</c> claim types every downstream consumer reads regardless of provider; stamped by <see cref="ClaimNormalizer"/>.</summary>
public static class NormalizedClaimTypes
{
    /// <summary>Auth scheme that signed the user in (e.g. <c>"GitHub"</c>) — stamped at sign-in, read to pick a profile.</summary>
    public const string Provider = "wt:provider";

    /// <summary>Provider-stable user identifier (the provider's subject / account id).</summary>
    public const string UserId = "wt:user_id";

    /// <summary>Verified email address, when the provider supplies one.</summary>
    public const string Email = "wt:email";

    /// <summary>Human-friendly display name, when the provider supplies one.</summary>
    public const string DisplayName = "wt:display_name";

    /// <summary>Provider handle / login (e.g. GitHub login, Discord username), when the provider supplies one.</summary>
    public const string Username = "wt:username";

    /// <summary>Absolute URL to the user's avatar, when available or synthesizable.</summary>
    public const string Avatar = "wt:avatar";
}
