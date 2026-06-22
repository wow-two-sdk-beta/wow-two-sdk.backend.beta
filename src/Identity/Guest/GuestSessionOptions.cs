using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Guest;

/// <summary>Tunable inputs for the guest-id cookie; keep <see cref="CookieName"/> in sync with <c>CurrentUserOptions.GuestCookieName</c>.</summary>
public sealed class GuestSessionOptions
{
    /// <summary>Name of the HttpOnly guest-id cookie. Default <c>user-id</c>.</summary>
    public string CookieName { get; set; } = "user-id";

    /// <summary>How long the guest cookie persists. Default 400 days (Chrome's persistent-cookie cap, so a guest sticks as long as the browser allows).</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(400);

    /// <summary>SameSite policy for the cookie. Default <see cref="SameSiteMode.Lax"/>.</summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;
}
