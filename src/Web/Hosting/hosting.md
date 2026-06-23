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

## SPA single-host serving

Serve a React/Vite (or any) SPA bundle from the same host as the API. `UseSpaHosting` serves the static
bundle (call early); `MapSpaFallback` 404s unmatched `/api/*` as JSON and falls every other unmatched
route back to the shell (call after your endpoints).

```csharp
var app = builder.Build();

app.UseSpaHosting();      // default document + static files — before auth/endpoints so assets short-circuit
app.UseApiDefaults();     // SDK pipeline
app.MapControllers();     // your endpoints first, so real routes win
app.MapSpaFallback();     // /api/* → JSON 404; everything else → index.html
```

`SpaHostingOptions`: `ApiPathPrefix` (default `/api`), `FallbackFile` (default `index.html`),
`ServeDefaultFiles` (default `true`). Configure via the optional callback on either call:
`app.MapSpaFallback(o => o.ApiPathPrefix = "/v1");`.
