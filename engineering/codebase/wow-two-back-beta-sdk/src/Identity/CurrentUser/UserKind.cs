namespace WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;

/// <summary>Refers to how <see cref="ICurrentUserService"/> identifies the calling principal.</summary>
public enum UserKind
{
    /// <summary>No identity — neither an authenticated principal nor a guest cookie is present.</summary>
    Anonymous,

    /// <summary>A cookie-provisioned guest — identified but not registered (see <see cref="Guest.Services.IGuestSessionService"/>).</summary>
    Guest,

    /// <summary>A registered, authenticated account.</summary>
    User,
}
