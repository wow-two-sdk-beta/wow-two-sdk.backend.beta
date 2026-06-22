# Meta — `AddApiDefaults` / `UseApiDefaults`

> The one-import boot floor. Namespace `WoW.Two.Sdk.Backend.Beta` (root) — one `using` lights it up.

```csharp
using WoW.Two.Sdk.Backend.Beta;

var builder = WebApplication.CreateBuilder(args);
builder.AddApiDefaults(o =>
{
    o.ValidatorAssemblies.Add(typeof(Program).Assembly);   // optional
    o.CorsOrigins.Add("https://app.example.com");          // optional
});

var app = builder.Build();
app.UseApiDefaults();
app.MapGet("/", () => "ok");
app.Run();
```

## What it wires

| Side | Concerns |
|---|---|
| `AddApiDefaults` | Serilog (`UseSerilogConventional`) · TimeProvider · OTel tracing + metrics + OTLP · health checks · proxy-aware hosting · OpenAPI · trace-aware ProblemDetails · validation exception handler · per-IP rate limit · output cache · Brotli/Gzip compression · CORS (when origins given) · FluentValidation scan (when assemblies given) |
| `UseApiDefaults` | forwarded headers · OWASP secure headers · CORS · rate limiter · output cache · compression · OpenAPI endpoint · `/health` |

Every concern has an off-flag on `ApiDefaultsOptions`; defaults are all-on.

## Deliberately NOT included

Auth (`AddJwtBearerAuthentication`, OAuth providers, OTP), mediator, and data — they need per-app
decisions (keys, assemblies, connection strings). Add them between the two calls as usual.

## See also

- Root [README.md](../../README.md) — per-area composition when you need more control
- [Package registry](../../docs/package-registry.md)
