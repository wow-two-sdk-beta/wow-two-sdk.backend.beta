using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;

/// <summary>Tunable inputs for cookie/claims-based current-user resolution; keep <see cref="GuestCookieName"/> in sync with <see cref="Guest.GuestSessionOptions.CookieName"/>.</summary>
public sealed class CurrentUserOptions
{
    /// <summary>Name of the guest-id cookie read when no authenticated principal is present. Default <c>user-id</c>.</summary>
    public string GuestCookieName { get; set; } = "user-id";

    /// <summary>Claim type carrying the registered account id on an authenticated principal. Default <see cref="ClaimTypes.NameIdentifier"/>.</summary>
    public string SubjectClaimType { get; set; } = ClaimTypes.NameIdentifier;
}
