# WoW.Two.Sdk.Backend.Beta.Testing.Auth

> Test-auth bypass for E2E (`TestAuthHandler` + `AddTestAuth`), cookie round-tripping (`CookieExtraction`), and a handler-level current-user stub (`TestCurrentUser`).

Part of the `WoW.Two.Sdk.Backend.Beta.Testing` package. Lets tests authenticate without real OAuth and lift `Secure` cookies the test host's `http://` transport won't auto-attach.

## Test-auth bypass (E2E)

Register the test scheme as the default inside a `WebApiTestHost<T>` so every request authenticates as a fixed identity:

```csharp
using WoW.Two.Sdk.Backend.Beta.Testing;
using WoW.Two.Sdk.Backend.Beta.Testing.Auth;

var host = new WebApiTestHost<Program>
{
    // shaped for ConfigureServicesHook — registers test auth as the default scheme
    ConfigureServicesHook = TestAuthServiceCollectionExtensions.UseTestUser(o =>
    {
        o.UserId = "u-123";
        o.Email  = "tester@example.com";
        o.Roles  = ["admin"];
    }),
};

var resp = await host.CreateClient().GetAsync("/api/me"); // already authenticated
```

Composing with other hook logic:

```csharp
ConfigureServicesHook = services =>
{
    services.RemoveAll<IGoogleTokenVerifier>();
    services.AddScoped<IGoogleTokenVerifier, FakeVerifier>();
    services.AddTestAuth(o => o.UserId = "u-123"); // call directly when composing
};
```

The handler stamps the SDK's canonical `wt:*` claims (`TestClaimTypes`, mirroring `Identity.Claims.NormalizedClaimTypes`) plus `ClaimTypes.Name` / `ClaimTypes.Role`, so the SDK's `ClaimsPrincipalExtensions` (`GetUserId()`, `GetEmail()`, …) and `[Authorize(Roles = …)]` both resolve. `AddTestAuth` runs after the app's own auth and overrides the default scheme.

## Cookie extraction

```csharp
var signIn = await client.PostAsync("/api/identity/guest", null);

var cookie = CookieExtraction.ExtractCookie(signIn, "app-auth"); // value or null
var authed = host.CreateClient().AttachCookie(signIn, "app-auth"); // lift + re-attach in one step
// or: host.CreateClient().AttachCookie("app-auth", cookie);
```

## Current-user stub (unit tests)

For handler/service tests that depend on the current user but don't need a web host:

```csharp
var current = new TestCurrentUser(Guid.NewGuid(), TestUserKind.Member);
current.GetCurrentUserId(); // Guid? — signature-compatible with IAuditCurrentUserAccessor
```

`TestCurrentUser.GetCurrentUserId()` matches the SDK's `Data.EntityFrameworkCore.Audit.IAuditCurrentUserAccessor` contract. This package is self-contained (no core-lib reference), so it does not implement that interface directly — an app's test project binds it at the seam:

```csharp
services.AddSingleton<IAuditCurrentUserAccessor>(new TestCurrentUser());
```

## See also

- `WebApiTestHost<T>` — the host whose `ConfigureServicesHook` seam these helpers plug into.
- `Identity.Claims.NormalizedClaimTypes` (core lib) — the canonical `wt:*` claims `TestClaimTypes` mirrors.
- `Identity.Cookies.AddCookieAuthentication` (core lib) — the real cookie scheme `TestAuthHandler` substitutes under test.
