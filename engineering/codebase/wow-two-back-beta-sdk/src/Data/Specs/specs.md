# Data.Specs

One provider-neutral description of an entity's storage — table, key, columns, indexes, concurrency, soft delete,
tenant — written once and translated by each data-access mapper. EF Core maps it today (`EntityFrameworkCore/Specs`).

```csharp
public sealed class OrderSpec : IEntitySpecConfiguration<Order>
{
    public void Configure(EntitySpecBuilder<Order> builder)
    {
        builder.ToTable("sales_orders");
        builder.Property(o => o.Number).HasColumnName("order_no").IsRequired().HasMaxLength(20);
        builder.Property(o => o.Total).HasPrecision(12, 2);
        builder.HasIndex(o => o.Number).IsUnique().HasName("ux_orders_number");
        builder.HasConcurrencyToken(o => o.Stamp, ConcurrencyTokenKind.Stamp);
        builder.Ignore(o => o.Display);
    }
}

builder.Services.AddEntitySpecs(typeof(OrderSpec).Assembly);                // or AddEntitySpecs(r => r.Add<Order>(b => …))
```

- Marker interfaces fill unset parts: `IHasTableName` → table, `IKeyedEntity` → key `Id`, `IVersioned` → `Counter`,
  `IHasXmin` → `Xmin`, `IRowVersioned` → `RowVersion`, `ISoftDeletable` → soft delete, `IHasTenant` → tenant.
- Concurrency kinds: `Counter` (incremented on save), `Stamp` (replaced with a fresh GUID on save), `RowVersion`
  (SQL Server), `Xmin` (PostgreSQL).
- A mapper that cannot express a feature follows `Data:Specs:Unsupported`: `Throw` (default) names every feature while
  building the mapping; `Skip` leaves them out and logs each.
