# EF Core repositories

> Thin generic CRUD repository over a DbContext. Id-keyed. Verb convention: **Create / Update / Delete / Get** — uniform across providers.

Namespace: `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Repositories`
Contracts: `WoW.Two.Sdk.Backend.Beta.Data.Abstractions` (`IRepository<TEntity, TId>`, `IReadRepository<TEntity, TId>` — zero-dep).

## Why these verbs

- **Create** — bring a new entity into existence (persist a new row). *Not* "Add" — `Add` is reserved ecosystem-wide for membership/collection ops (add user to group).
- **Get** — the universal read. Composes: `GetById`, `GetAll` (and future `GetByFilter`). Not `List` (breaks on `ListById`), not `Fetch` (implies remote).
- **Update / Delete** — as expected; plus id-based `DeleteByIdAsync`.

## Surface

```csharp
// read
Task<TEntity?>              GetByIdAsync(TId id, ct);
Task<IReadOnlyList<TEntity>> GetAllAsync(ct);
Task<bool>                  ExistsAsync(TId id, ct);
Task<int>                   CountAsync(ct);
// write
Task<TEntity> CreateAsync(TEntity e, ct);          // returns the entity (store-generated values populated)
Task          CreateRangeAsync(IEnumerable<TEntity> es, ct);
Task          UpdateAsync(TEntity e, ct);
Task          DeleteAsync(TEntity e, ct);
Task<bool>    DeleteByIdAsync(TId id, ct);          // false if no such row
```

Entities must implement `IKeyedEntity<TId>`. Reads honor global query filters (e.g. the SDK soft-delete filter). Each write persists immediately (`SaveChangesAsync`).

## Tracked writes

Load, modify and save the same tracked entity instance. Assign accepted properties or invoke domain operations,
then pass that instance to `UpdateAsync`; the repository lets EF retain original concurrency values and
property-level change tracking without calling `DbSet.Update`.

```csharp
var product = await repository.GetByIdAsync(id, ct);
product!.Name = request.Name;
await repository.UpdateAsync(product, ct);
```

Do not pass `product with { Name = request.Name }` while `product` is tracked. The repository rejects that
duplicate-key replacement instead of merging it, detaching the original or clearing the tracker.

A detached instance remains an explicitly supported update path when no same-key instance is tracked.
`UpdateAsync` attaches it through `DbSet.Update`, which marks its complete mapped state as modified; use it only
when that full-state write is the intended operation.

## Usage

```csharp
builder.Services.AddEfRepositories<AppDb>();

public sealed class ProductsService(IRepository<Product, Guid> repo)
{
    public Task<Product> Add(Product p, CancellationToken ct) => repo.CreateAsync(p, ct);
    public Task<Product?> Find(Guid id, CancellationToken ct) => repo.GetByIdAsync(id, ct);
}
```

### Custom query methods

Subclass for entity-specific reads, then register the concrete type:

```csharp
public sealed class ProductRepository(AppDb db) : EfRepository<Product, Guid>(db)
{
    public Task<List<Product>> InStock(CancellationToken ct) =>
        Set.Where(p => p.Stock > 0).ToListAsync(ct);
}

builder.Services.AddEfRepository<ProductRepository, Product, Guid>();
```

## Scope (deliberately thin)

Covers id-based CRUD — the 80% case. **Complex/composable queries** (filter + sort + page + include reuse) are written as plain LINQ in a subclass, or as Dapper SQL (with `SqlNamingMapper`) on the hot path. The SDK does **not** ship a Specification abstraction: it's `IQueryable`-bound and can't lower to Dapper, so it would split the query story across providers. If composable specs are ever needed, they'll arrive as a standalone opt-in package.
