# Data.EntityFrameworkCore.Specs

The EF Core mapper for `Data/Specs`. `AppDbContextBase` applies the registered `EntitySpecRegistry` on its own —
to the entities the context maps (a `DbSet` or a configuration), never adding others; other contexts call
`modelBuilder.ApplyEntitySpecs(registry, Database.ProviderName, mode)`.

| Spec | EF Core |
|---|---|
| table · key · column · required · length · precision · Unicode | `ToTable` · `HasKey` · `HasColumnName` · `IsRequired` · `HasMaxLength` · `HasPrecision` · `IsUnicode` |
| default · computed · generation · ignore | `HasDefaultValueSql` · `HasComputedColumnSql` · `ValueGenerated…` · `Ignore` |
| index | `HasIndex(…).IsUnique().HasDatabaseName().HasFilter()` |
| `Counter` · `Stamp` | concurrency token; `AppDbContextBase` increments or replaces it on save |
| `RowVersion` · `Xmin` | `IsRowVersion()` on SQL Server · `xmin`/`xid` on Npgsql; other providers → unsupported |
| soft delete | query filter `!IsDeleted` |

- Specs apply after the context's `IEntityTypeConfiguration`s and before the SDK conventions, so a spec facet wins
  over the same facet configured in EF code.
