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

Covers id-based CRUD — the 80% case. **Complex/composable queries** (filter + sort + page + include reuse) are written as plain LINQ in a subclass, or as Dapper SQL (with `SqlNaming`) on the hot path. The SDK does **not** ship a Specification abstraction: it's `IQueryable`-bound and can't lower to Dapper, so it would split the query story across providers. If composable specs are ever needed, they'll arrive as a standalone opt-in package.
