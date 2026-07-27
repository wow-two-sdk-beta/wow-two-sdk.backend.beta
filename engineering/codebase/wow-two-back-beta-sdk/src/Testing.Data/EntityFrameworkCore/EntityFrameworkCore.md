# Testing.Data — EntityFrameworkCore

Repoint an app's EF Core context off its production provider onto a test provider.

- `RemoveAllForDbContext<T>()` — strips a context's provider registrations, including the internal options-configuration (matched by open-generic name).
- `RepointDbContext<T>(configure)` — strip + re-add in one call.

## Example

```csharp
builder.ConfigureTestServices(services =>
{
    services.RepointDbContext<AppDbContext>(o => o.UseSqlite(sharedConnection).UseSnakeCaseNamingConvention());
    services.DisableBespokeMigrator(); // schema comes from EnsureCreated, not the bespoke migrator
});
```
