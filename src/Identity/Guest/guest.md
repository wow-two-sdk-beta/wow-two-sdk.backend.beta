# Identity.Guest

Idempotent anonymous-session cookie — gives an unregistered visitor a stable `Guid` so their work
(codes, drafts, carts) belongs to *them* before they ever sign in. No user model required.

| Seam | Default |
|---|---|
| `IGuestSession` | `CookieGuestSession` — `EnsureGuest()` returns the existing cookie's Guid or mints + appends one (at most once per request); `Clear()` deletes it after sign-in |

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
