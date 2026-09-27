# Cache invalidation across hosts

> PostgreSQL notifications that reach every host only when the writing transaction commits.

```csharp
// Every host that caches: listen and evict through ICacheRepository.
builder.Services.AddHybridCaching();
builder.Services.AddPostgresCacheInvalidation(options => options.ConnectionString = connectionString);

// The writer: queue the invalidation inside the write's transaction.
await using var transaction = await db.Database.BeginTransactionAsync(ct);
code.Destination = request.Destination;
await db.SaveChangesAsync(ct);
await db.Database.PublishCacheKeyInvalidationAsync($"code:{code.Slug}", cancellationToken: ct);
await transaction.CommitAsync(ct);   // the notification leaves now; a rollback discards it
```

- Notifications travel through the database every host already shares; no broker or Redis is required.
- `PublishCacheTagInvalidationAsync` evicts a whole tag; `ICacheRepository` tags map one to one.
- Each (re)subscription evicts everything first: notifications sent while a host was disconnected are lost.
- Keep a lifetime on cached entries; it bounds staleness if a host misses a notification.
- Replace `CacheRepositoryInvalidationHandler` by registering another `ICacheInvalidationHandler` first.
- A host that caches live objects in `IMemoryCache` calls `AddMemoryCacheInvalidationHandler()`; tags there evict everything.
- PostgreSQL only; payloads stay under 8,000 bytes.
