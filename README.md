# wow-two-sdk.backend.beta

> Beta-forever .NET 9 backend SDK — wraps the .NET ecosystem behind opinionated `AddWowTwo*` registrations so a `Program.cs` becomes 1–2 liners instead of 100.

## Status

| Phase | Bundle | Status |
|---|---|---|
| P0 | Testing scaffold (parallel track) | ✅ 12 packages shipped |
| P1 — foundation | `time`, `errors`, `results`, `validation`, `serialization`, `guards`, `value-objects` | ✅ 7 packages shipped |
| P1 — observability | `logging`, `tracing`, `metrics`, `healthchecks`, `otlp`, `prometheus`, `azure-monitor`, `datadog` | ✅ 8 packages shipped |
| P1 — web | `hosting`, `openapi`, `problemdetails`, `ratelimit`, `outputcache`, `secureheaders`, `cors`, `compression`, `versioning` | ✅ 9 packages shipped |
| P2 | request pipeline + auth | planned |
| P3 | persistence + outbound | planned |
| P4 | distributed essentials | planned |
| P5 | SaaS-shaped (tenancy + AI + flags) | planned |
| P6 | heavy domain extensions | planned |

**36 packages shipped**, 150+ planned. See [`docs/conventions/package-registry.md`](./docs/conventions/package-registry.md).

## Quick start (once P1 ships)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddWowTwoBackendBeta();   // one call wires the curated baseline
var app = builder.Build();
app.Run();
```

## Read these first

- [`docs/analysis/philosophy/ideas.md`](./docs/analysis/philosophy/ideas.md) — encyclopedia of the .NET ecosystem (no verdicts)
- [`docs/analysis/philosophy/targets.md`](./docs/analysis/philosophy/targets.md) — verdicts (DONE / NOW / NEXT / LATER / MAYBE / SKIP / LOCKED)
- [`docs/conventions/package-layout.md`](./docs/conventions/package-layout.md) — repo + per-package shape
- [`docs/conventions/documentation.md`](./docs/conventions/documentation.md) — three-layer doc strategy

## Build

```bash
cd src
ulimit -n 65535          # macOS — avoid EMFILE
export MSBUILDDISABLENODEREUSE=1
dotnet restore WoW.Two.Sdk.Backend.Beta.slnx -m:1
dotnet build WoW.Two.Sdk.Backend.Beta.slnx --no-restore -m:1
```

## License

MIT.
