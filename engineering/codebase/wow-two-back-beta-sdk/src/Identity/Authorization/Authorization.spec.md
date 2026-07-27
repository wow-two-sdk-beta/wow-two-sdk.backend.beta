# Authorization — spec

*Last updated: 2026-06-21*

> **Concrete API + usage.** Updated when public surface changes.

## NuGet

```
WoW.Two.Sdk.Backend.Beta.Identity.Authorization
```

## Quick start

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using WoW.Two.Sdk.Backend.Beta.Identity.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPrincipalAllowlist(o =>
    {
        // Leave empty for OPEN (single-admin). Add usernames to lock down:
        // o.Allowed.Add("sultonbek");
    })
    .AddDefaultDenyAuthorization(CookieAuthenticationDefaults.AuthenticationScheme, withAllowlist: true);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.Run();
```

## Public API

### `PrincipalAllowlistServiceCollectionExtensions`

| Method | Returns | Notes |
|---|---|---|
| `AddPrincipalAllowlist(this IServiceCollection, Action<AllowlistOptions>)` | `IServiceCollection` | Binds options + registers `AllowlistAuthorizationHandler` as `IAuthorizationHandler` |

### `DefaultDenyServiceCollectionExtensions`

| Member | Type / Returns | Notes |
|---|---|---|
| `const string PolicyName` | `"DefaultDeny"` | Named policy + fallback policy id |
| `AddDefaultDenyAuthorization(this IServiceCollection, string scheme, bool withAllowlist = false)` | `IServiceCollection` | `RequireAuthenticatedUser()` (+ `AllowlistRequirement` when `withAllowlist`) on `scheme`; registers named policy **and** `FallbackPolicy` |

### `AllowlistOptions` (record)

| Property | Type | Default | Notes |
|---|---|---|---|
| `ClaimType` | `string` | `"wt:username"` | Seam: `Identity/Claims` `NormalizedClaimTypes.Username` |
| `Allowed` | `ISet<string>` | empty (`OrdinalIgnoreCase`) | **Empty ⇒ OPEN** (any authenticated principal) |
| `CaseInsensitive` | `bool` | `true` | Claim-value comparison casing |

### Other public types

- `AllowlistRequirement : IAuthorizationRequirement` — the allowlist requirement.
- `AllowlistAuthorizationHandler : AuthorizationHandler<AllowlistRequirement>` — its handler.

## Semantics

| `Allowed` | Principal | Result |
|---|---|---|
| empty | authenticated | ✅ pass (OPEN — single-admin default) |
| empty | anonymous | ❌ (default-deny still needs auth) |
| non-empty | claim value ∈ `Allowed` | ✅ pass |
| non-empty | claim value ∉ `Allowed` / claim absent | ❌ fail |

`CaseInsensitive = true` ⇒ `OrdinalIgnoreCase`; `false` ⇒ `Ordinal`. Multiple `ClaimType` claims: any match succeeds.

## Usage examples

### Lock down to named admins

```csharp
services.AddPrincipalAllowlist(o =>
{
    o.Allowed.Add("sultonbek");
    o.Allowed.Add("ops-bot");
});
```

### Key on a different claim, case-sensitive

```csharp
services.AddPrincipalAllowlist(o =>
{
    o.ClaimType = "email";
    o.CaseInsensitive = false;
    o.Allowed.Add("admin@example.com");
});
```

### Anonymous opt-out (health check)

```csharp
app.MapHealthChecks("/health").AllowAnonymous(); // required — fallback policy denies otherwise
```

## See also

- Standard: [Authorization.standard.md](./Authorization.standard.md)
- Folder doc: [authorization.md](./authorization.md)
- ASP.NET Core authorization: <https://learn.microsoft.com/aspnet/core/security/authorization/introduction>
- Policy-based authorization: <https://learn.microsoft.com/aspnet/core/security/authorization/policies>
