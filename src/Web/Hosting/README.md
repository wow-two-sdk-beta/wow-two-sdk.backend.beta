# WoW.Two.Sdk.Backend.Beta.Web.Hosting

> ASP.NET Core hosting plumbing — forwarded headers + request decompression with sane defaults.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.Hosting
```

## Usage

```csharp
builder.Services.AddProxyAwareHosting();

var app = builder.Build();
app.UseProxyAwareHosting();   // adds forwarded headers + request decompression — call early
```
