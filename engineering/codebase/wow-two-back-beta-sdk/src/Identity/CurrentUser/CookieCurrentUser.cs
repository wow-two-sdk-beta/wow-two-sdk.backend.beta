using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;

/// <summary>Read-only <see cref="ICurrentUser"/> that resolves a tri-state principal from the request: authenticated (subject claim), guest (guest cookie), or anonymous.</summary>
/// <remarks>Resolves once per request and caches the result; never writes a cookie — use <see cref="Guest.IGuestSession"/> for provisioning.</remarks>
public sealed class CookieCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly CurrentUserOptions _options;
    private (Guid? Id, UserKind Kind)? _resolved;

    /// <summary>Creates the resolver over the request accessor and resolution options.</summary>
    /// <param name="accessor">Accessor for the ambient <see cref="HttpContext"/>.</param>
    /// <param name="options">Guest-cookie name and subject-claim type.</param>
    public CookieCurrentUser(IHttpContextAccessor accessor, IOptions<CurrentUserOptions> options)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(options);
        _accessor = accessor;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Guid? Id => Resolve().Id;

    /// <inheritdoc />
    public UserKind Kind => Resolve().Kind;

    private (Guid? Id, UserKind Kind) Resolve()
    {
        if (_resolved.HasValue) return _resolved.Value;

        var http = _accessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext — ICurrentUser is only valid within a request scope.");

        // Registered account: the authenticated principal carries the account id in its subject claim.
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var subject = http.User.FindFirst(_options.SubjectClaimType)?.Value;
            var userId = Guid.TryParse(subject, out var parsed) ? parsed : (Guid?)null;
            _resolved = (userId, UserKind.User);
            return _resolved.Value;
        }

        if (http.Request.Cookies.TryGetValue(_options.GuestCookieName, out var raw)
            && Guid.TryParse(raw, out var guestId))
        {
            _resolved = (guestId, UserKind.Guest);
            return _resolved.Value;
        }

        _resolved = (null, UserKind.Anonymous);
        return _resolved.Value;
    }
}
