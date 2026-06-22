# WoW.Two.Sdk.Backend.Beta.Foundation.Configuration

> Env-var-overlay settings loader — binds an appsettings section (named after the settings type), then overlays `[EnvironmentVariable]`-marked properties from environment variables. Empty env vars are treated as absent; required properties throw when still unset.

## Usage

### Mark the overlaid properties

```csharp
public sealed class DatabaseConnectionSettings
{
    [EnvironmentVariable("DB_CONNECTION", required: true)]
    public string ConnectionString { get; set; } = null!;
}
```

### Load directly

```csharp
var settings = ConfigurationLoader.Load<DatabaseConnectionSettings>(configuration);
// section name defaults to typeof(T).Name ("DatabaseConnectionSettings"); pass a name to override
```

### Register into DI (binds + overlays, then exposes IOptions<T>)

```csharp
builder.Services.AddEnvironmentOverlaidOptions<DatabaseConnectionSettings>(builder.Configuration);
// resolve via the settings type directly or via IOptions<DatabaseConnectionSettings>
```

## Behavior

- Section name = `typeof(T).Name` unless overridden.
- Env-var value wins over the appsettings-bound value when present and the property is writable.
- An empty or whitespace-only env var is treated as null and does not override.
- A property marked `required: true` with no value after binding and overlay throws `InvalidOperationException`.
- The environment-variable name is the consumer's choice — the SDK bakes in none.
