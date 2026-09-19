# Testing.Data — EntityFrameworkCore

Two seams: repoint an app's context onto a test provider, and run a whole suite against one real database.

## Repoint an existing context

- `RemoveAllForDbContext<T>()` — strips a context's provider registrations, including the internal options-configuration (matched by open-generic name).
- `RepointDbContext<T>(configure)` — strip + re-add in one call while preserving SDK-registered interceptors.

```csharp
builder.ConfigureTestServices(services =>
{
    services.RepointDbContext<AppDbContext>(o => o.UseSqlite(sharedConnection).UseSnakeCaseNamingConvention());
    services.DisableBespokeMigrator(); // schema comes from EnsureCreated, not the bespoke migrator
});
```

## `RelationalTestDb<TContext>`

A provider-switchable test database — Postgres container (Respawn reset) or in-memory SQLite — behind one API.

```csharp
public sealed class AppTestDb : RelationalTestDb<AppDbContext>
{
    public AppTestDb() : base(DatabaseProvider.Postgres) { }

    protected override void ApplyConventions(DbContextOptionsBuilder builder) => builder.UseSnakeCaseNamingConvention();

    protected override AppDbContext CreateContext(DbContextOptionsBuilder<AppDbContext> builder) => new(builder.Options);
}

[CollectionDefinition("app")]
public sealed class AppTestCollection : ICollectionFixture<AppTestDb>;

[Collection("app")]
public sealed class WidgetTests(AppTestDb db) : RelationalTestBase<AppTestDb, AppDbContext>(db)
{
    [Fact]
    public async Task Writes_a_row() { await using var context = TestDb.NewContext(); /* … */ }
}
```

| Member | Use it for |
|---|---|
| `NewContext()` | a context built straight from options — fast, but runs **no** registration code |
| `ApplyProvider(builder)` | point any builder at the test DB (provider + `ApplyConventions`) |
| `ApplyConventions(builder)` | override to add naming conventions / always-on interceptors — applied to **every** path, DI included |
| `OpenConnectionAsync()` | a second, real `DbConnection` — the Dapper tier and cross-connection visibility assertions |
| `ResetAsync()` | truncate between tests (`RelationalTestBase` calls it for you) |
| ctor taking `PostgresFixture` | share one container across a whole collection instead of one per class |

### Testing the registration, not bypassing it

`NewContext()` builds `DbContextOptions` directly, so it never runs the interceptor auto-wire loop or the pooling
branch inside `AddEntityFrameworkCore`. Anything asserting *which interceptors are attached*, or pooled-context
behaviour, must resolve the context from DI:

```csharp
services.AddEfInterceptor<MyInterceptor>();
services.AddTestEntityFrameworkCore(testDb);            // routes through AddEntityFrameworkCore (pooled by default)
var context = provider.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
```

### SQLite caveat

`OpenConnectionAsync()` throws on SQLite: the connection string is `DataSource=:memory:`, so a second connection
opens a **different, empty** database and anything asserted through it passes on nothing. Pin `Provider` to Postgres
for suites needing `xmin`, `FOR UPDATE`, real savepoint semantics, or a second connection.
