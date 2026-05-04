# WoW.Two.Sdk.Backend.Beta.Web.OpenApi

> Microsoft.AspNetCore.OpenApi (.NET 9 first-party) wiring.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.OpenApi
```

## Usage

```csharp
builder.Services.AddWowTwoOpenApi();

var app = builder.Build();
app.MapWowTwoOpenApi();   // -> /openapi/v1.json by default
```

## See also

- [Microsoft.AspNetCore.OpenApi docs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi)
