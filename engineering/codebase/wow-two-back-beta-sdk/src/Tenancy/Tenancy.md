# Tenancy

*Per-request tenant resolution + ambient context, with per-row (shared-DB) isolation on the existing `IHasTenant<string>` contract.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Tenancy`. Builds on the Data vector's `IHasTenant<TTenantId>` contract; the SDK convention is string tenant ids (subdomain/header/claim are strings).

## Surface

| Folder | Surface | Role |
|---|---|---|
| `Core/` | `AddTenancy(o => …)`, `UseTenantResolution()` | Resolve tenant (header/route/claim/subdomain) → ambient context |
| `Core/` | `ITenantContext` / `AmbientTenantContext`, `ITenantRepository` / `InMemoryTenantRepository`, `TenantInfo` | AsyncLocal-backed current tenant + tenant registry |
| `PerRow/` | `AddTenantRowStamping()`, `ModelBuilder.ApplyTenantFilter(ctx)` | Stamp and isolate EF writes + isolate EF reads via query filter |
| `Data/Dapper/Repositories/` | `DapperRepository<TEntity,TId>` | Stamp and isolate generated Dapper CRUD when `ITenantContext` is registered |

## Quickstart

```csharp
builder.Services
    .AddTenancy(o =>
    {
        o.UseClaim = true;                     // authenticated tenant_id claim
        o.KnownTenants.Add(new TenantInfo { Id = "acme", Name = "Acme Inc" });
    })
    .AddTenantRowStamping();                    // auto-stamp TenantId on inserts

app.UseAuthentication();
app.UseTenantResolution();   // after auth (for claim resolution), before endpoints

// In your DbContext.OnModelCreating(mb) — mb is filtered per current tenant:
protected override void OnModelCreating(ModelBuilder mb)
{
    base.OnModelCreating(mb);
    mb.ApplySoftDeleteFilter();
    mb.ApplyTenantFilter(_tenantContext);   // inject ITenantContext into the context
}
```

Entities opt in by implementing `IHasTenant<string>` (from `Data.Abstractions`).

## Design notes

- `ITenantContext` is an **AsyncLocal-backed singleton** — readable from singleton EF interceptors and query filters, and correct with DbContext pooling (the filter re-reads the ambient value per query).
- Claim resolution is the default. Header resolution requires a trusted gateway that overwrites the header; subdomain resolution requires host validation.
- The middleware clears ambient tenant state before and after every request, including exceptional exits.
- Inserts overwrite any supplied `TenantId`; updates and deletes verify the stored row tenant before writing.
- Generic Dapper CRUD stamps tenant writes and includes the tenant in every generated read/write predicate.
- Hand-written Dapper SQL owns its tenant predicate; implementing `IHasTenant<string>` alone cannot scope arbitrary SQL.
- No tenant in scope → write enforcement and the filter are inert (all rows) — reserve this for explicit system/admin paths.
- The `"Tenant"` query filter is a **named** filter (EF Core 10), so it co-exists with the default soft-delete filter.
- Resolution order: claim → route → header → subdomain (enable per `TenancyConventionOptions`).

## Roadmap (not yet built)

- Per-DB tenancy (connection-string-per-tenant `DbContext` factory) · per-schema · Finbuckle adapter · per-tenant cache-key prefix (pairs with Caching) · per-tenant config overlay.
