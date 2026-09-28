# Identity.Core

Our **own** identity system (not a wrapper over `Microsoft.AspNetCore.Identity`), exposed as orthogonal lego slices —
compose only what an app needs. Core is the mandatory slice: the user entity + user store + normalizer + the
`UserAccountService` facade. Entities carry ASP.NET-Identity-shaped columns and persist through the Data layer.

> Build order: `engineering/planning/identity/identity-architecture.md` §9; slice track: `identity-slices.md`.
> Every slice below is opt-in through the builder `AddUserAccounts` returns.

## Quick start

```csharp
// 1. host the identity schema on your DbContext
public sealed class AppDbContext(DbContextOptions options) : AppDbContextBase(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyIdentitySchema<IdentityUser, IdentityRole, Guid>();   // 9 tables + normalized-name unique indexes
    }
}

// 2. register core + the EF store
services.AddUserAccounts<IdentityUser>(o => o.User.RequireUniqueEmail = true)
        .AddEntityFrameworkStores<AppDbContext>();

// 3. use the facade
var service = sp.GetRequiredService<UserAccountService<IdentityUser, Guid>>();
var result  = await service.CreateAsync(new IdentityUser { UserName = "alice", Email = "alice@x.io" });
var alice   = await service.FindByNameAsync("ALICE");   // case-insensitive via the normalizer
```

## Slices

| Builder call | Service | Folder |
|---|---|---|
| `AddEntityFrameworkStores<TContext>()` | `UserAccountService`, `UserClaimsPrincipalFactory` | `./` |
| `AddArgon2Passwords()` · `AddBreachedPasswordCheck()` | `UserPasswordService` | `Passwords/` |
| `AddUserTokens(o => o.SigningKey = …)` | `UserTokenIssuer` | `Tokens/` |
| `AddEmailConfirmation()` | `UserEmailService` | `Emails/` |
| `AddLockout()` | `UserLockoutService` | `Lockout/` |
| `AddSecurityStampValidation()` | `SecurityStampValidator` + cookie/JWT hooks | `SecurityStamps/` |
| `AddRoles<TRole>()` | `RoleService`, `UserRoleService` | `Roles/` |
| `AddUserClaims()` | `UserClaimService` | `UserClaims/` |
| `AddExternalLogins()` | `UserLoginService` | `Logins/` |
| `AddTwoFactor()` | `UserTwoFactorService` | `TwoFactor/` |
| `AddPhoneNumbers()` | `UserPhoneService` | `Phones/` |
| `AddRefreshTokens()` | `RefreshTokenService` | `RefreshTokens/` |
| `AddPasskeys(o => …)` | `UserPasskeyService`, `SignInService.PasskeySignInAsync` | `Passkeys/` |
| `AddEmailSignIn(o => …)` | `UserEmailSignInService` (single-use link or code over OTP) | `EmailSignIn/` |
| `AddAccountEndpoints()` + `MapUserAccountEndpoints<TUser>()` | account HTTP API | `Endpoints/` |
| `AddSignIn()` | `SignInService` | `SignIn/` |

Table-backed slices (`AddRoles`, `AddUserClaims`, `AddExternalLogins`, `AddTwoFactor`, `AddRefreshTokens`, `AddPasskeys`) register their EF repositories
only when called after `AddEntityFrameworkStores`; otherwise the host registers its own repositories.

## Notes

- **Schema owned by EF fluent config** (`ApplyIdentitySchema`), not the bespoke SQL migrator — the identity schema is a
  library schema consumers migrate via EF. Casing comes from `UseSnakeCaseNamingConvention()`.
- Uniqueness is enforced **both** in the facade (`DuplicateUserName`/`DuplicateEmail`) and by the DB unique index.
- A capability that isn't registered is **absent** — resolving its service fails at resolution, rather than
  silently no-op'ing. Optional collaborators (lockout inside sign-in, tokens inside passwords) switch steps on or off.
- Credential changes (password, email, role or claim removal, 2FA changes, unlinking) rotate the security stamp.
- Deleting a user removes its role, claim, login and token rows in the same save; the schema has no foreign keys.
- `IdentityResult.ToValidationError()` turns failures into field errors (code + values) that error translation matches.
- Entry point is **`AddUserAccounts`** (not `AddIdentityCore` as the deep-dive drafted) — ASP.NET's own
  `AddIdentityCore<TUser>` extension is always in scope via the shared framework and would collide.
- User updates preserve the loaded tracked instance. A detached replacement with the same key is rejected while
  the original is tracked; a detached full-state update remains supported in a fresh context.

## See also

- Architecture + build order: [`engineering/planning/identity/identity-architecture.md`](../../../../../planning/identity/identity-architecture.md)
- Data layer this persists through: `../../Data/` (`AppDbContextBase`, `AddEntityFrameworkCore`, audit interceptor).
