namespace WoW.Two.Sdk.Backend.Beta.Identity.Guest.Services;

/// <summary>Defines creation, reuse and clearing of the guest-id cookie within a request.</summary>
public interface IGuestSessionService
{
    /// <summary>Ensures a guest identity exists for the current request, returning the stable Guid (cookie appended at most once).</summary>
    Guid EnsureGuest();

    /// <summary>Clears the guest cookie from the response — called after sign-in, once the auth cookie is the identity.</summary>
    void Clear();
}
