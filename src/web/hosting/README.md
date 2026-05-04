# WoW.Two.Sdk.Backend.Beta.Web.Hosting

> ASP.NET Core hosting plumbing — forwarded headers + request decompression with sane defaults.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.Hosting
```

## Usage

```csharp
builder.Services.AddWowTwoHosting();

var app = builder.Build();
app.UseWowTwoHosting();   // adds forwarded headers + request decompression — call early
```
