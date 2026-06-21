# Identity.Authorization

Provider-agnostic ASP.NET Core authorization primitives for self-hosted, single-admin (or small-allowlist) APIs.

## Primitives

- **Principal allowlist** — `AllowlistRequirement` + `AllowlistAuthorizationHandler`, configured via `AllowlistOptions`. Gates to an allowed set keyed on a claim.
  - `ClaimType` default `"wt:username"` (seam: `Identity/Claims` `NormalizedClaimTypes.Username`).
  - **Open-when-empty:** `Allowed` empty ⇒ any *authenticated* principal passes. Non-empty ⇒ principal must carry a `ClaimType` claim whose value ∈ `Allowed` (case-insensitive by default).
- **Default-deny** — `AddDefaultDenyAuthorization(scheme, withAllowlist)` builds `RequireAuthenticatedUser()` (+ allowlist) on **one named scheme**, registers it under `PolicyName` (`"DefaultDeny"`) **and** sets it as `FallbackPolicy` ⇒ endpoints without `[Authorize]`/`[AllowAnonymous]` are denied by default.
  - Single named scheme (the cookie) → clean **401**, not an external-IdP **302**.
  - `/health`-style anonymous endpoints **must** use `[AllowAnonymous]`.

**Compose:** `withAllowlist: true` makes the fallback require *authenticated* **and** *allowlisted*; empty allowlist keeps it authenticated-only.

## Canonical wiring order

```csharp
builder.Services
    .AddCookieAuthentication(o => o.ChallengeMode = AuthChallengeMode.Api) // app cookie (clean 401)
    .Add{Provider}Authentication(/* ... */)                               // external IdP (OAuth/OIDC)
    .AddClaimNormalization()                                              // → wt:username (Identity/Claims)
    .AddPrincipalAllowlist(o =>
    {
        // empty Allowed ⇒ OPEN (single admin). Add usernames to lock down:
        // o.Allowed.Add("sultonbek");
    })
    .AddDefaultDenyAuthorization(CookieAuthenticationDefaults.AuthenticationScheme, withAllowlist: true);
```

`AddClaimNormalization` is the planned `Identity/Claims` seam emitting the `wt:username` claim the allowlist keys on.

See also: `Identity/Cookies` (named scheme + `AuthChallengeMode.Api`), `Identity/Policies` (role-in-scope gating — orthogonal), `Authorization.spec.md`, `Authorization.standard.md`.
