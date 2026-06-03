# WoW.Two.Sdk.Backend.Beta.Web.Cors

> CORS policy presets.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.Cors
```

## Usage

```csharp
builder.Services.AddDefaultCorsPolicy("https://example.com", "https://app.example.com");

var app = builder.Build();
app.UseCors(CorsServiceCollectionExtensions.DefaultPolicyName);
```

Pass no origins → permissive `AllowAnyOrigin` (use only in dev / sandboxes).
