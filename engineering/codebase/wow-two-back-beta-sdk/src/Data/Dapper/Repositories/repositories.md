# Dapper repositories

> Thin generic CRUD repository on the Dapper hot path. Same `Create / Update / Delete / Get` vocabulary as the EF repo — SQL generated from `IHasTableName` + `SqlNaming` + reflected properties.

Namespace: `WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories`
Contracts: `IRepository<TEntity, TId>` / `IReadRepository<TEntity, TId>` (same as EF — interchangeable at the call site).

## Requirements

The entity must declare **both** `IKeyedEntity<TId>` and `IHasTableName`:

```csharp
public sealed class Product : IKeyedEntity<Guid>, IHasTableName
{
    public static string TableName => "products";
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public long PriceUsd { get; set; }     // ← maps to column price_usd
}
```

## Usage

```csharp
builder.Services.AddDataSourceConnectionFactory();   // or AddDbConnectionFactory<TFactory>()
builder.Services.AddDapperRepository<Product, Guid>();

public sealed class ProductsService(IRepository<Product, Guid> repo)
{
    public Task<Product>  Add(Product p, CancellationToken ct) => repo.CreateAsync(p, ct);
    public Task<Product?> Find(Guid id, CancellationToken ct)  => repo.GetByIdAsync(id, ct);
}
```

`AddDapperRepository` calls `AddDapperConventions()` for you (idempotent) — so snake_case columns bind to PascalCase properties. Without it, multi-word columns silently read as defaults.

## Generated SQL

| Method | SQL |
|---|---|
| `GetByIdAsync` | `SELECT * FROM products WHERE id = @id` |
| `GetAllAsync` | `SELECT * FROM products` |
| `ExistsAsync` | `SELECT EXISTS (SELECT 1 FROM products WHERE id = @id)` |
| `CountAsync` | `SELECT COUNT(*) FROM products` |
| `CreateAsync` | `INSERT INTO products (id, name, price_usd) VALUES (@Id, @Name, @PriceUsd)` |
| `UpdateAsync` | `UPDATE products SET name = @Name, price_usd = @PriceUsd WHERE id = @Id` |
| `DeleteByIdAsync` | `DELETE FROM products WHERE id = @id` |

Columns come from every public read-write property, cased via `SqlNaming.ColumnCase` (default snake).

## Identity / computed / store-generated columns

Subclass and override the exclusion hooks, then register the concrete type:

```csharp
public sealed class OrderRepository(IDbConnectionFactory f) : DapperRepository<Order, long>(f)
{
    // DB assigns the id (serial / identity) — keep it out of INSERT
    protected override IReadOnlyCollection<string> ExcludedOnInsert => [nameof(Order.Id)];
}

builder.Services.AddDapperRepository<OrderRepository, Order, long>();
```

`ExcludedOnUpdate` defaults to `[Id]` (the key is never in the SET list). `ExcludedOnInsert` defaults to empty.

## EF vs Dapper repo — which?

Same contracts, swap freely. EF repo for change-tracked write workflows; Dapper repo for the read-heavy hot path (no tracking overhead, raw SQL). Complex queries stay hand-written either way — subclass and add methods. The SDK ships no `IQueryable`/Specification layer (it can't lower to Dapper).

## Provider notes

- **Postgres** (native `uuid`, `int`, text) — works as-is.
- **SQLite** stores `Guid` as TEXT; Dapper can't auto-cast `string`→`Guid` on read. Use native-typed keys (`int`/`long`) or register a Guid type handler if targeting SQLite with Guid keys.
