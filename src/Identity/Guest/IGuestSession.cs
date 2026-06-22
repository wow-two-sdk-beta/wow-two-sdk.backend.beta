namespace WoW.Two.Sdk.Backend.Beta.Identity.Guest;

/// <summary>Idempotent guest-provisioning service — returns the existing guest cookie's Guid, or mints a new one and appends the cookie.</summary>
public interface IGuestSession
{
    /// <summary>Ensures a guest identity exists for the current request, returning the stable Guid (cookie appended at most once).</summary>
    Guid EnsureGuest();

    /// <summary>Clears the guest cookie from the response — called after sign-in, once the auth cookie is the identity.</summary>
    void Clear();
}
