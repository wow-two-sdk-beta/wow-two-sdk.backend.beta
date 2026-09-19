using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using WoW.Two.Sdk.Backend.Beta.Identity.Guest.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Guest.Services;

/// <summary>Provides creation, reuse and clearing of the guest-id cookie within a request.</summary>
/// <remarks>Provisions at most once per request; the cookie is HttpOnly, Secure, and essential (an ownership capability, exempt from consent gating — not tracking).</remarks>
public sealed class CookieGuestSessionService : IGuestSessionService
{
    private readonly IHttpContextAccessor _accessor;
    private readonly GuestSessionOptions _options;
    private readonly GuestCookieSerializer _serializer;

    /// <summary>Creates the service over the request accessor and cookie options.</summary>
    /// <param name="accessor">Accessor for the ambient <see cref="HttpContext"/>.</param>
    /// <param name="options">Cookie name, lifetime, and SameSite policy.</param>
    /// <param name="protection">Application-scoped Data Protection provider.</param>
    /// <param name="clock">Clock used for the protected expiry.</param>
    public CookieGuestSessionService(IHttpContextAccessor accessor, GuestSessionOptions options,
        IDataProtectionProvider protection, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(options);
        _accessor = accessor;
        _options = options;
        _serializer = new GuestCookieSerializer(protection, clock);
    }

    /// <inheritdoc />
    public Guid EnsureGuest()
    {
        var http = _accessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext — IGuestSessionService is only valid within a request scope.");

        if (http.ReadGuest(_options.CookieName, _serializer) is { } existing)
            return existing;

        var issued = Guid.NewGuid();
        var token = _serializer.Serialize(issued, _options.CookieName, _options.Lifetime);
        http.Response.Cookies.Append(_options.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = _options.SameSite,
            IsEssential = true,     // ownership capability, not tracking — exempt from consent gating.
            MaxAge = _options.Lifetime,
            Path = "/",
        });

        http.SetGuest(_options.CookieName, issued);
        return issued;
    }

    /// <inheritdoc />
    public void Clear()
    {
        var http = _accessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext — IGuestSessionService is only valid within a request scope.");

        // Expire with the same attributes it was written with, or strict browsers ignore the deletion of a Secure cookie.
        http.Response.Cookies.Delete(_options.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = _options.SameSite,
            Path = "/",
        });
        http.SetGuest(_options.CookieName, null);
    }
}
