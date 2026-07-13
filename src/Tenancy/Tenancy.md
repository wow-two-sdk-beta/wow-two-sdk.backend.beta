# Tenancy

*Per-request tenant resolution + ambient context, with per-row (shared-DB) isolation on the existing `IHasTenant<string>` contract.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Tenancy`. Builds on the Data vector's `IHasTenant<TTenantId>` contract; the SDK convention is string tenant ids (subdomain/header/claim are strings).

## Surface

| Folder | Surface | Role |
|---|---|---|
| `Core/` | `AddTenancy(o => …)`, `UseTenantResolution()` | Resolve tenant (header/route/claim/subdomain) → ambient context |
| `Core/` | `ITenantContext` / `AmbientTenantContext`, `ITenantStore` / `InMemoryTenantStore`, `TenantInfo` | AsyncLocal-backed current tenant + tenant registry |
| `PerRow/` | `AddTenantRowStamping()`, `ModelBuilder.ApplyTenantFilter(ctx)` | Stamp tenant on insert + isolate reads via query filter |

## Quickstart

```csharp
builder.Services
    .AddTenancy(o =>
    {
        o.UseSubdomain = true;                 // acme.example.com → "acme"
        o.UseHeader = true;                    // X-Tenant-Id: acme
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
- No tenant in scope → stamping is skipped and the filter is inert (all rows) — for system/admin paths.
- The `"Tenant"` query filter is a **named** filter (EF Core 10), so it co-exists with the default soft-delete filter.
- Resolution order: header → route → claim → subdomain (enable per `TenancyConventionOptions`).

## Roadmap (not yet built)

- Per-DB tenancy (connection-string-per-tenant `DbContext` factory) · per-schema · Finbuckle adapter · per-tenant cache-key prefix (pairs with Caching) · per-tenant config overlay.
