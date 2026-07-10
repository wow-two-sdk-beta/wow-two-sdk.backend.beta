# Identity (own, sliced) — architecture

*Last updated: 2026-06-10*

> Build our **own** identity system that copies ASP.NET Core Identity's most important features but
> exposes them as **orthogonal lego slices** — compose user store + password + OTP + Telegram +
> Google + Microsoft + roles + JWT in host extensions, taking only what a given app needs.
> Reverses the v1 "no `IAuthUser` entity" decision (see `system/sessions/backend-beta-build/auth-extraction-analysis.md` §4).

---

## 1. Goal & rationale

- **Own it** — don't wrap `Microsoft.AspNetCore.Identity`. Its `UserManager` is a god-object that needs every store wired; you can't cleanly drop roles or graft a Telegram login without fighting it.
- **Lego** — each capability is one store interface + one default EF impl + one `.AddX()` builder call. Apps opt in per slice.
- **Compatible** — keep entity columns shaped like ASP.NET Identity's 7 tables so existing DBs migrate trivially and tokens interop.
- **Reuse** — plug the already-shipped primitives (Argon2, OTP, JWT issuance, OAuth×16, role policy, MFA) into the slices rather than reimplementing them.

## 2. Which ASP.NET Identity features we copy

| Feature | Verdict | Our slice |
|---|---|---|
| User CRUD + find by id/name/email/phone | ✅ copy | `identity/core` — `IUserStore` |
| Password hash + verify + change | ✅ copy | reuse shipped `UseArgon2PasswordHasher` behind `IUserPasswordStore` |
| Password reset tokens | ✅ copy | `IUserTokenStore` + token provider |
| Email confirmation | ✅ copy | `IUserEmailStore` + confirmation token |
| Phone confirmation / phone login | ✅ copy | `IUserPhoneStore` + reuse shipped `IOtpService` |
| External logins (Google/MS/Telegram/…) | ✅ copy | `IUserLoginStore` + reuse shipped `oauth.*` / OTP-Telegram |
| Lockout (failed-attempt counting) | ✅ copy | `IUserLockoutStore` |
| Security stamp (invalidate on cred change) | ✅ copy | `IUserSecurityStampStore` — **also gives us token revocation** |
| Two-factor (TOTP / WebAuthn / recovery codes) | ✅ copy | `IUserTwoFactorStore` + reuse shipped `TotpService` / `AddFido2WebAuthn` |
| Roles + role claims | ✅ copy | `IUserRoleStore` + `IRoleStore`; authz via shipped `AddRolePolicy` |
| Per-user claims | ✅ copy | `IUserClaimStore` |
| Normalizer (upper-invariant keys) | ✅ copy | `ILookupNormalizer` |
| Claims-principal factory | ✅ copy | `IUserClaimsPrincipalFactory<TUser>` |
| `UserManager` god-object | ❌ skip | replaced by a thin `UserAccountManager` that composes only registered slices |
| `SignInManager` (cookie-coupled) | 🟡 split | `ISignInService` slice; cookie vs JWT chosen by `.AddCookieSignIn()` / `.AddJwtIssuance()` |
| Identity UI / Razor scaffolding | ❌ skip | API-only; UI is the frontend lib's job |

## 3. Architecture — sliced stores

```
                 ┌─────────────────────────────────────────────┐
                 │  UserAccountManager<TUser>  (thin facade)    │
                 │  feature-detects which slices are registered │
                 └─────────────────────────────────────────────┘
                                   │ depends on (all optional except core)
   ┌───────────────┬──────────────┼───────────────┬───────────────┬──────────────┐
   ▼               ▼              ▼                ▼               ▼              ▼
IUserStore   IUserPassword   IUserEmail      IUserLogin     IUserLockout   IUserRole
(core)       Store           Store           Store          Store          + IRoleStore
   │            │ Argon2       │ confirm tok   │ ext logins    │              │ + RolePolicy
   ▼            ▼              ▼                ▼               ▼              ▼
IUserPhone   IUserToken     IUserClaim     IUserSecurity   IUserTwoFactor   (each slice:
Store         Store          Store          StampStore       Store           interface +
   │ OTP        │ reset/2FA    │              │ revocation     │ TOTP/WebAuthn  default EF impl +
   ▼            ▼              ▼                ▼               ▼              .AddX() builder)
        every default impl persists via the existing Data layer (§5)
```

- **Core is mandatory**; every other slice is opt-in.
- The facade calls a slice only if its interface resolved; otherwise the capability is absent (no silent no-op — `NotSupportedException` with a "did you call `.AddX()`?" message).

## 4. Entities (ASP.NET Identity-column-compatible)

Generic key `TKey` (default `Guid`). Each implements our Data abstractions so audit/soft-delete/concurrency/tenancy come for free.

| Entity | Key columns (Identity-compatible) | Implements |
|---|---|---|
| `IdentityUser<TKey>` | UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount | `IKeyedEntity<TKey>`, `IAuditable`, opt `ISoftDeletable`, opt `IHasTenant<T>` |
| `IdentityRole<TKey>` | Name, NormalizedName, ConcurrencyStamp | `IKeyedEntity<TKey>`, `IAuditable` |
| `IdentityUserRole<TKey>` | UserId, RoleId | `IEntity` (composite key) |
| `IdentityUserClaim<TKey>` | UserId, ClaimType, ClaimValue | `IKeyedEntity<int>` |
| `IdentityRoleClaim<TKey>` | RoleId, ClaimType, ClaimValue | `IKeyedEntity<int>` |
| `IdentityUserLogin<TKey>` | LoginProvider, ProviderKey, ProviderDisplayName, UserId | `IEntity` (composite key) — **external logins** |
| `IdentityUserToken<TKey>` | UserId, LoginProvider, Name, Value | `IEntity` (composite key) — reset/2FA/recovery |

- `ConcurrencyStamp` (string GUID) is kept for Identity compatibility; optionally also map `IHasXmin`/`IRowVersioned` for true DB-level optimistic concurrency.
- `IHasTenant<T>` on the user is a **bonus over ASP.NET Identity** — multi-tenant users out of the box (composes with the planned `tenancy` track).

## 5. Data-layer assessment — have vs build

**Foundation (already shipped, sufficient):**

| Need | Shipped |
|---|---|
| Entity keying | `IKeyedEntity<TId>` |
| Audit columns | `IAuditable` (`CreatedAt`/`UpdatedAt`) + audit `SaveChangesInterceptor` |
| Soft delete | `ISoftDeletable` + interceptor + query filter |
| Concurrency | `IRowVersioned` / `IHasXmin` / `IVersioned` |
| Multi-tenant | `IHasTenant<TTenantId>` |
| CRUD base | `IRepository<TEntity,TId>` / `IReadRepository<TEntity,TId>` |
| DbContext base | `AppDbContextBase` + `AddEntityFrameworkCore<T>` |
| Providers | Postgres / SqlServer / Sqlite (+ Cosmos) |
| Migrations | EF runner + DbUp runner |
| Naming | snake_case convention, casing authority (`Naming`) |

**Identity-specific (must build):**

| Gap | What |
|---|---|
| The 7 entities | §4 — as POCOs implementing the Data abstractions |
| EF mappings | `modelBuilder.ApplyIdentitySchema<TUser,TRole,TKey>()` — keys, indexes (normalized-name unique), relationships |
| `IdentityDbContextBase<TUser,TRole,TKey>` | extends `AppDbContextBase`; apps inherit or call the model-builder extension |
| Store implementations | one EF impl per slice, mostly thin over `DbSet`/`IRepository` |
| Token providers | data-protection-backed (or HMAC) generators for email/reset/2FA tokens |

**Verdict: the generic data layer is a sufficient foundation; identity needs its own entity + mapping + store slice on top. No changes to the Data abstractions are required.**

## 6. Store interfaces (the slices)

`identity/core`: `IUserStore<TUser,TKey>` (create/update/delete/find-by-id/find-by-name) · `ILookupNormalizer` · `IUserClaimsPrincipalFactory<TUser>`.

Optional slices (each its own interface, default EF impl, `.AddX()`):
`IUserPasswordStore` · `IUserEmailStore` · `IUserPhoneStore` · `IUserLoginStore` · `IUserClaimStore` · `IUserRoleStore` + `IRoleStore` · `IUserTokenStore` · `IUserSecurityStampStore` · `IUserLockoutStore` · `IUserTwoFactorStore` · `IUserAuthenticatorKeyStore` (+ recovery codes).

## 7. The lego — host-extension composition

`AddIdentityCore<TUser>()` returns an `IdentityBuilder`; every `.AddX()` is optional and additive.

```csharp
services.AddIdentityCore<AppUser>(o =>
    {
        o.Password.MinLength = 12;
        o.Lockout.MaxFailedAttempts = 5;
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()   // persistence on the existing Data layer

    // password + breach check
    .AddArgon2Passwords()                        // reuses shipped UseArgon2PasswordHasher
    .AddBreachedPasswordCheck()                  // optional HIBP k-anonymity

    // confirmation + passwordless
    .AddEmailConfirmation()
    .AddPhoneOtpLogin()                          // reuses shipped IOtpService

    // external logins — add only what the app needs
    .AddTelegramLogin()                          // reuses Otp.Telegram link flow
    .AddGoogleLogin(cfg["Google:Id"], cfg["Google:Secret"])      // reuses oauth.google
    .AddMicrosoftLogin(cfg["MS:Id"], cfg["MS:Secret"])           // reuses oauth.microsoft

    // authz + 2FA + lifecycle
    .AddRoles<AppRole>()                         // role store; authz via AddRolePolicy
    .AddTwoFactor()                              // reuses TotpService / AddFido2WebAuthn
    .AddSecurityStampValidation()                // invalidate tokens on cred change
    .AddLockout()

    // session shape — pick one or both
    .AddJwtIssuance(o => { o.Issuer = "app"; o.SigningKey = cfg["Jwt:Key"]; })  // reuses jwt.issuance
    .AddCookieSignIn();                          // reuses cookies
```

Drop any line → that capability simply isn't present. A stateless microservice might call only `AddIdentityCore + AddEntityFrameworkStores + AddJwtIssuance`. A consumer SaaS adds email/phone/external/roles/2FA.

## 8. Reuse map — slices → already-shipped packages

| Slice | Plugs into (shipped) |
|---|---|
| password | `Identity/PasswordHashing/Argon2` (`UseArgon2PasswordHasher`) |
| phone / passwordless | `Identity/Otp` (`AddOtpService`) |
| Telegram login | `Identity/Otp/Telegram` (`AddTelegramOtpDelivery`) |
| Google/Microsoft/+13 logins | `Identity/OAuth/*` (`AddGoogleAuthentication`, …) |
| 2FA | `Identity/Mfa/Totp` (`TotpService`), `Identity/Mfa/WebAuthn` (`AddFido2WebAuthn`) |
| token mint | `Identity/Jwt/Issuance` (`AddJwtTokenIssuance`) |
| token validate | `Identity/Jwt` (`AddJwtBearerAuthentication`) |
| cookie sign-in | `Identity/Cookies` (`AddCookieAuthentication`) |
| role authz | `Identity/Policies` (`AddRolePolicy`) |
| persistence | `Data/*` (`AddEntityFrameworkCore`, audit + soft-delete interceptors) |

The rebuild is mostly **glue + the user model + the stores** — ~60% of the surface already exists as orthogonal packages.

## 9. Build order

1. ✅ **DONE 2026-07-10** — `identity/core` (`src/Identity/Core/`): 7 entities + `IUserStore`/`EfUserStore` + normalizer + options + `UserAccountManager` facade + `IdentityResult` + `ApplyIdentitySchema` (EF-owned schema, normalized-name unique indexes). Entry: **`AddUserAccounts<TUser>()`** → `IdentityBuilder.AddEntityFrameworkStores<TContext>()` (renamed from `AddIdentityCore` — collides with ASP.NET's shared-framework extension). Vertical create→find→delete + dup-reject green on SQLite (`Identity.Tests/`).
2. Password slice — `IUserPasswordStore` + Argon2 wire + set/verify/change.
3. Email + token slices — `IUserEmailStore` + `IUserTokenStore` + a token provider → confirmation + reset.
4. Lockout + security-stamp — `IUserLockoutStore` + `IUserSecurityStampStore` (unlocks revocation).
5. External-login slice — `IUserLoginStore` + `.AddGoogleLogin/.AddMicrosoftLogin/.AddTelegramLogin`.
6. Phone-OTP login — `IUserPhoneStore` + wire `IOtpService`.
7. Roles + claims — `IRoleStore` + `IUserRoleStore` + `IUserClaimStore` + principal factory.
8. Two-factor — `IUserTwoFactorStore` + authenticator key + recovery codes.
9. Sign-in service + JWT/cookie wiring — `ISignInService` orchestrating password→lockout→2FA→principal/token.
10. Dogfood: migrate Haven.Auth onto it.

## 10. Decisions

**Locked**
- Build own, don't wrap ASP.NET Identity (god-object incompatible with lego).
- Entity columns ASP.NET-Identity-compatible (trivial DB migration + token interop).
- Default key type `Guid`; `TKey` generic for `int`/`string`/`Ulid` consumers.
- Default impls persist via the existing Data layer; no new Data abstractions.
- Facade throws `NotSupportedException` (not silent no-op) when a slice is absent.

**Open**
- `identity/core` in the core meta vs a `WoW.Two.Sdk.Backend.Beta.Identity.Core` sub-area? (lean: sub-area folder, mono-lib for now.)
- Token providers: ASP.NET DataProtection vs our own HMAC + `TimeProvider`? (lean: HMAC, fewer deps, AOT-friendly.)
- Normalized-key uniqueness enforced in store vs DB index only? (lean: both.)
- `UserAccountManager` granularity — one facade vs per-slice managers (UserManager/RoleManager/SignInManager split)? (lean: one facade + `ISignInService`.)
- Recovery-code storage shape (hashed list in `IUserToken` vs dedicated table).

## 11. Security items this subsumes

From the security backlog (`../platform-planning.md`):
- **Token revocation** ⇒ `IUserSecurityStampStore` + stamp-validation middleware.
- **Account lockout** ⇒ `IUserLockoutStore` (native, not a wrap).
- **Breached-password check** ⇒ a password-validator slice.
- Still separate (not identity-owned): request-limits, CSRF, HTTPS/HSTS, SSRF guard, OpenAPI env-gate.

## Backlog — OAuth / claims primitives (post-baseline, 2026-06-21)

The external-login baseline (uniform OAuth×17 + X · claim normalizer · cookie API/MVC mode · allowlist + default-deny) shipped `10.0.29-beta`. Follow-ups:

- **Verify 6 uncertain claim profiles** — Twitter/X · Yandex · VKontakte · Amazon · Slack · Notion — against each lib's actual `ClaimActions` (the profiles are best-effort URNs; verification-only, no new code).
- **Settable-options sweep** — `CookieAuthOptions` was `init`-only so `AddCookieAuthentication(configure)` couldn't set it (`CS8852`); fixed (`init`→`set`). Audit `AllowlistOptions` · `ClaimNormalizationOptions` · `OtpOptions` for the same `configure`-can't-set trap.
- **Sync `targets.md`** — register the OAuth-baseline / claim-normalizer / cookie-mode / authorization vectors (paired source-of-truth rule).

## 12. See also

- `system/sessions/backend-beta-build/auth-extraction-analysis.md` — the v1 "no user model" decision this reverses (§4) + the OTP/issuance/policies seams now reused.
- `docs/package-registry.md` — shipped identity packages the slices plug into.
- `docs/analysis/philosophy/targets.md` — add the `identity/core` vector under P2 when this starts (paired source-of-truth rule).
- `../platform-planning.md` — backlog + roadmap home.
