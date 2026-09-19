namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Holds canonical <c>wt:*</c> claim types every downstream consumer reads regardless of provider; stamped by <see cref="ClaimMapper"/>.</summary>
public static class NormalizedClaimTypeConstants
{
    /// <summary>Holds auth scheme that signed the user in (e.g. <c>"GitHub"</c>) — stamped at sign-in, read to pick a spec.</summary>
    public const string Provider = "wt:provider";

    /// <summary>Holds provider-stable user identifier (the provider's subject / account id).</summary>
    public const string UserId = "wt:user_id";

    /// <summary>Holds verified email address, when the provider supplies one.</summary>
    public const string Email = "wt:email";

    /// <summary>Holds human-friendly display name, when the provider supplies one.</summary>
    public const string DisplayName = "wt:display_name";

    /// <summary>Holds provider handle / login (e.g. GitHub login, Discord username), when the provider supplies one.</summary>
    public const string Username = "wt:username";

    /// <summary>Holds absolute URL to the user's avatar, when available or synthesizable.</summary>
    public const string Avatar = "wt:avatar";
}
