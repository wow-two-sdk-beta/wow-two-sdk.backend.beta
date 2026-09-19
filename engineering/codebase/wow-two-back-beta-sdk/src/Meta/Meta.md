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
app.UseApiDefaults(pipeline =>
{
    pipeline.UseAuthentication();
    pipeline.UseAuthorization();
});
app.MapGet("/", () => "ok");
app.Run();
```

## What it wires

| Side | Concerns |
|---|---|
| `AddApiDefaults` | Serilog (`UseSerilogConventional`) · TimeProvider · OTel tracing + metrics + OTLP · health checks · proxy-aware hosting · OpenAPI · trace-aware ProblemDetails · validation exception handler · per-IP rate limit · output cache · Brotli/Gzip compression · CORS (when origins given) · FluentValidation scan (when assemblies given) |
| `UseApiDefaults` | forwarded headers · OWASP secure headers · compression · routing · CORS · optional identity seam · rate limiter · output cache · OpenAPI endpoint · `/health` |

Every concern has an off-flag on `ApiDefaultsOptions`; defaults are all-on.

## Deliberately NOT included

Auth (`AddJwtBearerAuthentication`, OAuth providers, OTP), mediator, and data — they need per-app
decisions (keys, assemblies, connection strings). Register auth before `Build()`, then place its
middleware through the `UseApiDefaults` callback so identity-aware policies see the principal.

## See also

- Root [README.md](../../../../../README.md) — per-area composition when you need more control
- [Package registry](../../../../architecture/package-registry.md)
