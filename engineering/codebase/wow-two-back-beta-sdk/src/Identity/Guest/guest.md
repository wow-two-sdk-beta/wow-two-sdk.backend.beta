# Identity.Guest

Idempotent anonymous-session cookie — gives an unregistered visitor a stable `Guid` so their work
(codes, drafts, carts) belongs to *them* before they ever sign in. No user model required.

| Seam | Default |
|---|---|
| `IGuestSessionService` | `CookieGuestSessionService` — `EnsureGuest()` returns the valid capability's Guid or mints + appends one (at most once per request); `Clear()` deletes it after sign-in |

```csharp
builder.Services.AddGuestSession(o =>
{
    o.CookieName = "user-id";              // keep in sync with AddCurrentUser
    o.Lifetime   = TimeSpan.FromDays(400); // Chrome's persistent-cookie cap
    o.SameSite   = SameSiteMode.Lax;
});

// in a request: stamp ownership on a newly created resource
var ownerId = _guest.EnsureGuest();

// after a successful sign-in: the auth cookie is now the identity
_guest.Clear();
```

- Cookie is `HttpOnly` + `Secure` + `IsEssential` (an ownership capability, not tracking — exempt from
  consent gating) + `Path=/`.
- Request-scoped: throws outside an HTTP request.
- Read the guest back (and tell guest from authenticated) via [`../CurrentUser`](../CurrentUser/current-user.md)
  — register both on the **same** cookie name.

- Cookies carry an authenticated capability, never a trusted raw GUID; raw/forged/expired values resolve as anonymous.
- `Clear()` masks the inbound cookie immediately for this request; `EnsureGuest()` creates a new identity after clearing.
- Configure durable ASP.NET Core Data Protection keys in persistent deployments. Share application name and key ring
  only between hosts that intentionally share guest identity. Key loss loses guest access; Clear is not server-side revocation.
- [Guest capability contract](GuestSession.spec.md) defines purpose isolation and expiry through `TimeProvider`.
