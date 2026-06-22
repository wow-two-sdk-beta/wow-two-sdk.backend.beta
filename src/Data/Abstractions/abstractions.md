# WoW.Two.Sdk.Backend.Beta.Data.Abstractions

> Entity contracts for persisted domain types. **Zero ORM deps** — reference from your Domain assembly without pulling EF Core.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Data.Abstractions
```

## Contract layers

The package layers in three tiers:

| Tier | Interfaces | Role |
|---|---|---|
| **1. Marker** | `IEntity` | Every persisted domain type. Empty marker. |
| **2. PK contract** | `IKeyedEntity<TId>` | Adds `Id` of type `TId`. Constraint: `TId : notnull, IEquatable<TId>`. |
| **3. Traits** | `IAuditable`, `IAuditableBy<TUserId>`, `ISoftDeletable`, `ISoftDeletableBy<TUserId>`, `IHasTenant<TTenantId>`, `IRowVersioned`, `IHasXmin`, `IVersioned` | Opt-in capabilities. All derive from `IEntity`. |

## Full reference

| Interface | Members | Wired by |
|---|---|---|
| `IEntity` | (marker) | Base for all persisted types |
| `IKeyedEntity<TId>` | `Id` | EF Core via `HasKey`; repositories; specifications |
| `ICreationAuditable` | `CreatedAt` | `…Data.EntityFrameworkCore.Audit` |
| `IModificationAuditable` | `UpdatedAt` | `…Data.EntityFrameworkCore.Audit` |
| `IAuditable` | (composite of the two above) | `…Data.EntityFrameworkCore.Audit` |
| `ICreationAuditableBy<TUserId>` | `CreatedBy` | `…Data.EntityFrameworkCore.Audit` |
| `IModificationAuditableBy<TUserId>` | `UpdatedBy` | `…Data.EntityFrameworkCore.Audit` |
| `IAuditableBy<TUserId>` | (composite of the two above) | `…Data.EntityFrameworkCore.Audit` |
| `ISoftDeletable` | `IsDeleted`, `DeletedAt` | `…Data.EntityFrameworkCore.SoftDelete` |
| `ISoftDeletableBy<TUserId>` | `DeletedBy` | `…Data.EntityFrameworkCore.SoftDelete` |
| `IHasTenant<TTenantId>` | `TenantId` | `…Tenancy.PerRow` (P5) |
| `IRowVersioned` | `RowVersion` | `…Data.EntityFrameworkCore.SqlServer` |
| `IHasXmin` | `Xmin` | `…Data.EntityFrameworkCore.Postgres` |
| `IVersioned` | `Version` | `…Data.EntityFrameworkCore` (any provider) |

## Usage

### Minimal entity

```csharp
public sealed record Channel : IKeyedEntity<Guid>
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string Name { get; init; }
}
```

### Auditable + soft-deletable

```csharp
public sealed record Channel : IKeyedEntity<Guid>, IAuditable, ISoftDeletable
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string Name { get; init; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

### Append-only entity (creation half only)

```csharp
public sealed record AuditLogEntry : IKeyedEntity<Guid>, ICreationAuditable, ICreationAuditableBy<Guid>
{
    public required Guid Id { get; init; }
    public required string Action { get; init; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    // no UpdatedAt / UpdatedBy — immutable after insert
}
```

### Max-stacked entity

```csharp
public sealed record Order :
    IKeyedEntity<Guid>, IAuditable, IAuditableBy<Guid>, ISoftDeletable, ISoftDeletableBy<Guid>,
    IHasTenant<Guid>, IHasXmin
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; set; }
    public required decimal Total { get; init; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public uint Xmin { get; set; }
}
```

### Strongly-typed ID (Vogen)

```csharp
[ValueObject<Guid>]
public readonly partial struct OrderId;

public sealed record Order : IKeyedEntity<OrderId>, IAuditable
{
    public required OrderId Id { get; init; }
    // …
}
```

## Design notes

- **`IEntity` is marker-only** — no `Id` member. Use `IKeyedEntity<TId>` for entities; reserve raw `IEntity` for projections / views that genuinely lack a single PK.
- **`TId : notnull, IEquatable<TId>`** — Guid, int, long, string, Vogen IDs all satisfy. Enables boxing-free PK equality.
- **No `TableName` on `IEntity`** — storage names belong in `IEntityTypeConfiguration<T>` (EF Core) or `[Table]` attribute (Dapper).
- **All traits derive from `IEntity`** — the marker propagates transitively; consumers rarely write `: IEntity` directly.
- **Audit splits by lifecycle phase** — `ICreationAuditable` / `IModificationAuditable` (and the `…By` actor variants) compose into `IAuditable` / `IAuditableBy<TUserId>`. Append-only entities (outbox, events, logs, raw ingestion) implement only the creation half — no phantom `UpdatedAt`.
- **`…By` user-id is a value type** — `IAuditableBy<TUserId>` / `ISoftDeletableBy<TUserId>` constrain `TUserId : struct` (Guid, int, long, Vogen struct).
- **Concurrency markers** — `IRowVersioned` (SqlServer, `byte[]`), `IHasXmin` (Postgres, `uint`), and `IVersioned` (portable numeric, `uint`) are separate; pick the one matching your provider.

## See also

- `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore` — base DbContext + scanner
- `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit` — wires the audit interfaces
- `WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SoftDelete` — wires the soft-delete interfaces
- `wow-two-ws/conventions/development/backend/persistence/entities.md` — entity-level documentation + trait usage rules
