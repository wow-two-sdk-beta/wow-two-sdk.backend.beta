namespace WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;

/// <summary>How the calling principal is identified, as resolved by <see cref="ICurrentUser"/>.</summary>
public enum UserKind
{
    /// <summary>No identity — neither an authenticated principal nor a guest cookie is present.</summary>
    Anonymous,

    /// <summary>A cookie-provisioned guest — identified but not registered (see <see cref="Guest.IGuestSession"/>).</summary>
    Guest,

    /// <summary>A registered, authenticated account.</summary>
    User,
}
