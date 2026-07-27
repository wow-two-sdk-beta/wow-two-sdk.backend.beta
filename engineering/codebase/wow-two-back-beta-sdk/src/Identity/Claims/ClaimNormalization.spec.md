# ClaimNormalization — spec

*Last updated: 2026-06-21*

> **Concrete API + usage.** Updated when public surface changes.

NuGet: `WoW.Two.Sdk.Backend.Beta` (mono-lib) · namespace `WoW.Two.Sdk.Backend.Beta.Identity.Claims`.

## Quick start

```csharp
using WoW.Two.Sdk.Backend.Beta.Identity.Claims;

builder.Services.AddClaimNormalization();                              // opt-in, after OAuth + cookie registration
builder.Services.AddClaimNormalization(o => o.AddProvider("Acme", acmeProfile));   // or configure

// read canonical claims (nullable) in a controller / handler:
public IActionResult Me() => Ok(new
{
    provider = User.GetProvider(), id = User.GetUserId(), email = User.GetEmail(),
    displayName = User.GetDisplayName(), username = User.GetUsername(), avatar = User.GetAvatar(),
});
```

## Public API

### `ClaimNormalizationServiceCollectionExtensions`

| Method | Returns | Notes |
|---|---|---|
| `AddClaimNormalization(this IServiceCollection, Action<ClaimNormalizationOptions>? configure = null)` | `IServiceCollection` | Registers `ClaimNormalizer` as `IClaimsTransformation` (`TryAddEnumerable`). Opt-in, idempotent. |

### `NormalizedClaimTypes` (static consts)

`Provider` = `wt:provider` · `UserId` = `wt:user_id` · `Email` = `wt:email` · `DisplayName` = `wt:display_name` · `Username` = `wt:username` · `Avatar` = `wt:avatar`.

### `ClaimsPrincipalExtensions`

`GetProvider()` · `GetUserId()` · `GetEmail()` · `GetDisplayName()` · `GetUsername()` · `GetAvatar()` — all `string?`, `null` when the claim is absent.

### `ClaimNormalizationOptions` (record)

| Member | Type | Default | Notes |
|---|---|---|---|
| `SynthesizeAvatars` | `bool` | `true` | Honor a profile's avatar synthesizer when no avatar claim is present. |
| `Profiles` | `IDictionary<string, ClaimProviderProfile>` | built-ins | Case-insensitive scheme → profile map, pre-seeded. |
| `AddProvider(string scheme, ClaimProviderProfile profile)` | `ClaimNormalizationOptions` | — | Add/override a profile; chainable. |

### `ClaimProviderProfile` (record)

Positional: `Scheme`, `UserIdClaims`, `EmailClaims`, `DisplayNameClaims`, `UsernameClaims`, `AvatarClaims` (all `IReadOnlyList<string>`, priority-ordered), `AvatarSynthesizer` (`Func<AvatarSynthesisContext, string?>?`).

### `AvatarSynthesisContext` (readonly record struct)

Positional: `Principal` (`ClaimsPrincipal`), `UserId` (`string?`), `Username` (`string?`).

### `ClaimProviderProfiles` (static)

| Method | Returns | Notes |
|---|---|---|
| `CreateDefault()` | `Dictionary<string, ClaimProviderProfile>` | Fresh, mutable, case-insensitive map of all built-ins. |
| `BuiltIn()` | `IEnumerable<ClaimProviderProfile>` | One per supported scheme. |

### `ClaimNormalizer` (sealed, `IClaimsTransformation`)

`Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal)` — adds `wt:*` claims; early-returns when already normalized, no `wt:provider`, or no matching profile.

## Custom provider (synthesized avatar)

```csharp
services.AddClaimNormalization(o => o.AddProvider("Acme", new ClaimProviderProfile(
    "Acme",
    UserIdClaims: [ClaimTypes.NameIdentifier],
    EmailClaims: [ClaimTypes.Email],
    DisplayNameClaims: ["urn:acme:name"],
    UsernameClaims: [ClaimTypes.Name],
    AvatarClaims: [],   // direct claim list; omit for synth-only
    AvatarSynthesizer: ctx => ctx.UserId is { } id ? $"https://cdn.acme.test/a/{id}.png" : null)));
```

## See also

- Standard: [`ClaimNormalization.standard.md`](./ClaimNormalization.standard.md)
- Folder lead: [`claims.md`](./claims.md)
- Underlying lib: [AspNet.Security.OAuth.Providers](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers)
