# WoW.Two.Sdk.Backend.Beta.Web.Versioning

> API versioning via `Asp.Versioning.*` with conventional defaults.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.Versioning
```

## Usage

```csharp
builder.Services.AddDefaultApiVersioning();

var versioned = app.NewVersionedApi();
var v1 = versioned.MapGroup("/v{version:apiVersion}").HasApiVersion(new ApiVersion(1, 0));
v1.MapGet("/hello", () => "v1");

var v2 = versioned.MapGroup("/v{version:apiVersion}").HasApiVersion(new ApiVersion(2, 0));
v2.MapGet("/hello", () => "v2");
```

Sources accepted: URL segment (`/v1/...`), header `api-version`, query `?api-version=1.0`.

## See also

- [Asp.Versioning](https://github.com/dotnet/aspnet-api-versioning)
