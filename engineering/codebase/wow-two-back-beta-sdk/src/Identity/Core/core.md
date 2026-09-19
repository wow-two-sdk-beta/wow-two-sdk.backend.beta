# Identity.Core

Our **own** identity system (not a wrapper over `Microsoft.AspNetCore.Identity`), exposed as orthogonal lego slices —
compose only what an app needs. Core is the mandatory slice: the user entity + user store + normalizer + the
`UserAccountService` facade. Entities carry ASP.NET-Identity-shaped columns and persist through the Data layer.

> Step 1 of the identity build order (see `engineering/planning/identity/identity-architecture.md`). Vertical shipped:
> create → find → delete a user. Password / email / lockout / roles / 2FA / sign-in are later slices on this schema.

## Quick start

```csharp
// 1. host the identity schema on your DbContext
public sealed class AppDbContext(DbContextOptions options) : AppDbContextBase(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyIdentitySchema<IdentityUser, IdentityRole, Guid>();   // 7 tables + normalized-name unique indexes
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

## Layout

| File | What |
|---|---|
| `IdentityUser.cs` / `IdentityRole.cs` | user + role entities (`IdentityUser<TKey>` + `IdentityUser : IdentityUser<Guid>`) |
| `IdentityRelations.cs` | user-role / user-claim / role-claim / user-login / user-token entities |
| `IdentitySchema.cs` | `ApplyIdentitySchema<TUser,TRole,TKey>()` — EF mapping (keys, unique indexes, lengths) |
| `NormalizeMapper.cs` | `INormalizeMapper` + upper-invariant default |
| `IUserRepository.cs` / `EfUserRepository.cs` | core store slice + EF impl |
| `UserAccountService.cs` | thin facade — normalize, enforce uniqueness, stamp, persist |
| `IdentityResult.cs` | `IdentityResult` / `IdentityError` |
| `IdentityBuilder.cs` / `IdentityCoreServiceCollectionExtensions.cs` | `AddIdentityCore` + `.AddEntityFrameworkStores` |

## Notes

- **Schema owned by EF fluent config** (`ApplyIdentitySchema`), not the bespoke SQL migrator — the identity schema is a
  library schema consumers migrate via EF. Casing comes from `UseSnakeCaseNamingConvention()`.
- Uniqueness is enforced **both** in the facade (`DuplicateUserName`/`DuplicateEmail`) and by the DB unique index.
- A capability that isn't registered is **absent** — resolving `UserAccountService` without `.AddEntityFrameworkStores`
  (or another `IUserRepository`) fails fast at resolution, rather than silently no-op'ing.
- Entry point is **`AddUserAccounts`** (not `AddIdentityCore` as the deep-dive drafted) — ASP.NET's own
  `AddIdentityCore<TUser>` extension is always in scope via the shared framework and would collide.
- User updates preserve the loaded tracked instance. A detached replacement with the same key is rejected while
  the original is tracked; a detached full-state update remains supported in a fresh context.

## See also

- Architecture + build order: [`engineering/planning/identity/identity-architecture.md`](../../../../../planning/identity/identity-architecture.md)
- Data layer this persists through: `../../Data/` (`AppDbContextBase`, `AddEntityFrameworkCore`, audit interceptor).
