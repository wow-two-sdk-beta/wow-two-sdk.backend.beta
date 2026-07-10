# Identity.Core

Our **own** identity system (not a wrapper over `Microsoft.AspNetCore.Identity`), exposed as orthogonal lego slices —
compose only what an app needs. Core is the mandatory slice: the user entity + user store + normalizer + the
`UserAccountManager` facade. Entities carry ASP.NET-Identity-shaped columns and persist through the Data layer.

> Step 1 of the identity build order (see `docs/planning/identity/identity-architecture.md`). Vertical shipped:
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
var manager = sp.GetRequiredService<UserAccountManager<IdentityUser, Guid>>();
var result  = await manager.CreateAsync(new IdentityUser { UserName = "alice", Email = "alice@x.io" });
var alice   = await manager.FindByNameAsync("ALICE");   // case-insensitive via the normalizer
```

## Layout

| File | What |
|---|---|
| `IdentityUser.cs` / `IdentityRole.cs` | user + role entities (`IdentityUser<TKey>` + `IdentityUser : IdentityUser<Guid>`) |
| `IdentityRelations.cs` | user-role / user-claim / role-claim / user-login / user-token entities |
| `IdentitySchema.cs` | `ApplyIdentitySchema<TUser,TRole,TKey>()` — EF mapping (keys, unique indexes, lengths) |
| `LookupNormalizer.cs` | `ILookupNormalizer` + upper-invariant default |
| `IUserStore.cs` / `EfUserStore.cs` | core store slice + EF impl |
| `UserAccountManager.cs` | thin facade — normalize, enforce uniqueness, stamp, persist |
| `IdentityResult.cs` | `IdentityResult` / `IdentityError` |
| `IdentityBuilder.cs` / `IdentityCoreServiceCollectionExtensions.cs` | `AddIdentityCore` + `.AddEntityFrameworkStores` |

## Notes

- **Schema owned by EF fluent config** (`ApplyIdentitySchema`), not the bespoke SQL migrator — the identity schema is a
  library schema consumers migrate via EF. Casing comes from `UseSnakeCaseNamingConvention()`.
- Uniqueness is enforced **both** in the facade (`DuplicateUserName`/`DuplicateEmail`) and by the DB unique index.
- A capability that isn't registered is **absent** — resolving `UserAccountManager` without `.AddEntityFrameworkStores`
  (or another `IUserStore`) fails fast at resolution, rather than silently no-op'ing.
- Entry point is **`AddUserAccounts`** (not `AddIdentityCore` as the deep-dive drafted) — ASP.NET's own
  `AddIdentityCore<TUser>` extension is always in scope via the shared framework and would collide.

## See also

- Architecture + build order: [`../../../docs/planning/identity/identity-architecture.md`](../../../docs/planning/identity/identity-architecture.md)
- Data layer this persists through: `../../Data/` (`AppDbContextBase`, `AddEntityFrameworkCore`, audit interceptor).
