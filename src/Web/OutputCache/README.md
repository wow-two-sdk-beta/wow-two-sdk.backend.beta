# WoW.Two.Sdk.Backend.Beta.Web.OutputCache

> `Microsoft.AspNetCore.OutputCaching` wiring with a default 60s policy.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.OutputCache
```

## Usage

```csharp
builder.Services.AddDefaultOutputCache();

var app = builder.Build();
app.UseOutputCache();

app.MapGet("/expensive", () => Compute()).CacheOutput(OutputCacheServiceCollectionExtensions.DefaultPolicyName);
```
