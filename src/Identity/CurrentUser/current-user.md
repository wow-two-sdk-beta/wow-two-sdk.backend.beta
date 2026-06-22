# Identity.CurrentUser

Read-only, tri-state view of the request's principal — **authenticated** / **guest** / **anonymous** —
in one injectable. Resolves once per request, never writes a cookie.

| Seam | Default |
|---|---|
| `ICurrentUser` | `CookieCurrentUser` — authenticated (subject claim) → guest (guest cookie) → anonymous |
| `UserKind` | `User` · `Guest` · `Anonymous` |

```csharp
builder.Services.AddCurrentUser(o =>
{
    o.GuestCookieName   = "user-id";              // match AddGuestSession
    o.SubjectClaimType  = ClaimTypes.NameIdentifier;
});

// in a handler / endpoint
switch (_currentUser.Kind)
{
    case UserKind.User:      /* _currentUser.Id is the account id */ break;
    case UserKind.Guest:     /* _currentUser.Id is the guest id   */ break;
    case UserKind.Anonymous: /* _currentUser.Id is null           */ break;
}
```

- Resolution order: an authenticated principal wins; else a parseable guest cookie; else anonymous.
- `Id` is the account id when authenticated, the guest id when guest, `null` when anonymous.
- Pair with [`../Guest`](../Guest/guest.md) (issues the guest cookie) on the **same** cookie name.
- Request-scoped: throws outside an HTTP request.
