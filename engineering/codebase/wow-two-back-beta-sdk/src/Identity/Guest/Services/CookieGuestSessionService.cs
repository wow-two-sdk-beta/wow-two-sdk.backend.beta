using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Guest.Services;

/// <summary>Provides creation, reuse and clearing of the guest-id cookie within a request.</summary>
/// <remarks>Provisions at most once per request; the cookie is HttpOnly, Secure, and essential (an ownership capability, exempt from consent gating — not tracking).</remarks>
public sealed class CookieGuestSessionService : IGuestSessionService
{
    private readonly IHttpContextAccessor _accessor;
    private readonly GuestSessionOptions _options;
    private Guid? _provisioned;

    /// <summary>Creates the service over the request accessor and cookie options.</summary>
    /// <param name="accessor">Accessor for the ambient <see cref="HttpContext"/>.</param>
    /// <param name="options">Cookie name, lifetime, and SameSite policy.</param>
    public CookieGuestSessionService(IHttpContextAccessor accessor, GuestSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(options);
        _accessor = accessor;
        _options = options;
    }

    /// <inheritdoc />
    public Guid EnsureGuest()
    {
        if (_provisioned.HasValue) return _provisioned.Value;

        var http = _accessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext — IGuestSessionService is only valid within a request scope.");

        if (http.Request.Cookies.TryGetValue(_options.CookieName, out var raw) && Guid.TryParse(raw, out var existing))
        {
            _provisioned = existing;
            return _provisioned.Value;
        }

        var issued = Guid.NewGuid();
        http.Response.Cookies.Append(_options.CookieName, issued.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = _options.SameSite,
            IsEssential = true,     // ownership capability, not tracking — exempt from consent gating.
            MaxAge = _options.Lifetime,
            Path = "/",
        });

        _provisioned = issued;
        return _provisioned.Value;
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
        _provisioned = null;
    }
}
