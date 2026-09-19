using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;

/// <summary>Provides the current tri-state principal from ambient claims and cookies.</summary>
/// <remarks>Resolves on each access so this singleton never retains one request's principal; never writes a cookie — use <see cref="Guest.Services.IGuestSessionService"/> for provisioning.</remarks>
public sealed class CookieCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;
    private readonly CurrentUserOptions _options;

    /// <summary>Creates the resolver over the request accessor and resolution options.</summary>
    /// <param name="accessor">Accessor for the ambient <see cref="HttpContext"/>.</param>
    /// <param name="options">Guest-cookie name and subject-claim type.</param>
    public CookieCurrentUserService(IHttpContextAccessor accessor, CurrentUserOptions options)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(options);
        _accessor = accessor;
        _options = options;
    }

    /// <inheritdoc />
    public Guid? Id => Resolve().Id;

    /// <inheritdoc />
    public UserKind Kind => Resolve().Kind;

    private (Guid? Id, UserKind Kind) Resolve()
    {
        var http = _accessor.HttpContext;
        if (http is null)
            return (null, UserKind.Anonymous);

        // Registered account: the authenticated principal carries the account id in its subject claim.
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var subject = http.User.FindFirst(_options.SubjectClaimType)?.Value;
            var userId = Guid.TryParse(subject, out var parsed) ? parsed : (Guid?)null;
            return (userId, UserKind.User);
        }

        if (http.Request.Cookies.TryGetValue(_options.GuestCookieName, out var raw)
            && Guid.TryParse(raw, out var guestId))
        {
            return (guestId, UserKind.Guest);
        }

        return (null, UserKind.Anonymous);
    }
}
