# Caching

*Two-tier caching over .NET HybridCache — L1 in-process + optional L2 Redis, with stampede protection and tag invalidation, behind a small `ICache` facade.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Caching`.

## Surface

| Folder | Surface | Role |
|---|---|---|
| `Core/` | `ICache`, `CacheEntryOptions`, `ICacheKeyBuilder` / `CacheKeyBuilder` | Provider-neutral facade + key convention |
| `Hybrid/` | `AddHybridCaching(configure?)`, `HybridCacheAdapter`, `HybridCacheConventionOptions` | HybridCache default (the `ICache` impl) |
| `Redis/` | `AddRedisDistributedCache(connStr, instanceName?)` | Redis L2 backend (auto-used by HybridCache) |
| `Memory/` | `AddInMemoryCaching()` | Plain `IMemoryCache` for trivial cases |

## Quickstart

```csharp
builder.Services
    .AddHybridCaching(o => o.DefaultExpiration = TimeSpan.FromMinutes(10))
    .AddRedisDistributedCache(cfg.GetConnectionString("redis")!, instanceName: "app:");  // omit → L1-only

public sealed class Catalog(ICache cache, ICacheKeyBuilder keys)
{
    public ValueTask<Product> GetAsync(int id, CancellationToken ct) =>
        cache.GetOrCreateAsync(
            keys.Build("product", id.ToString()),
            _ => LoadAsync(id, ct),
            new CacheEntryOptions { Expiration = TimeSpan.FromMinutes(30), Tags = ["catalog"] },
            ct);

    public ValueTask InvalidateCatalog(CancellationToken ct) => cache.RemoveByTagAsync("catalog", ct);
}
```

## Notes

- `GetOrCreateAsync` coalesces concurrent misses for the same key (stampede protection) — the factory runs once.
- Tags enable group eviction (`RemoveByTagAsync`) across both tiers.
- Register a Redis `IDistributedCache` (via `AddRedisDistributedCache`) and HybridCache uses it as L2 automatically — no other wiring.
- Defaults: 5 min total / 1 min L1 / 1 MB max payload — tune via `AddHybridCaching(o => …)`.

## Roadmap (not yet built)

- FusionCache alt (fail-safe / soft-timeout) · SqlServer / Cosmos L2 backends · per-tenant key-prefix decorator (pairs with the Tenancy vector) · ETag/OutputCache bridge (Web).
