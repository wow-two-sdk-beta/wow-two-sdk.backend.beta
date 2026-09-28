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
var settings = ConfigurationMapper.Load<DatabaseConnectionSettings>(configuration);
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

## Explicit environment contract

`UseEnvironmentAliases` replaces the environment-variable sources with a listed contract: each supported variable
maps to one configuration key, so an unrelated variable in the deployment can never override a setting.

```csharp
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.Configuration.UseEnvironmentAliases(new Dictionary<string, string>
{
    ["DB_CONNECTION"] = "DB_CONNECTION",
    ["GOOGLE_CLIENT_ID"] = "Auth:Google:ClientId",
});
```

- Only set, non-empty variables override; `urls`, `http_ports` and `https_ports` keep their hosting values.
- Sources added afterwards, such as test-host overrides, still win.
