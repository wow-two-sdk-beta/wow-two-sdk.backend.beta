# WoW.Two.Sdk.Backend.Beta

> Beta-forever .NET 9 backend SDK — **one class library, divided by folders**. Foundation, observability, web, mediator, identity, and data concerns ship in a single package.

## Why a mono-lib

Granular per-concern packages cost a create-csproj → wire-references → build-graph → version-bump cycle on every change. This package collapses that: one build, one push, fast iteration. The folder tree under `src/` is the division (`data/dapper/`, `foundation/errors/`, `identity/jwt/`, …); namespaces are unchanged (`WoW.Two.Sdk.Backend.Beta.Data.Dapper`, etc.).

Granular packages return once the surface is mature and saturated. Cost accepted until then: a large transitive closure (every DB driver, OpenTelemetry, Serilog, …).

Test helpers ship separately as **`WoW.Two.Sdk.Backend.Beta.Testing`** so xUnit / Testcontainers never enter a production reference.

## Layout

```
src/
├── WoW.Two.Sdk.Backend.Beta.csproj   ← this lib (globs every shipping folder)
├── foundation/  observability/  web/  mediator/  identity/  data/
└── testing/                          ← separate mono-lib (test helpers)
```

## Build

```bash
cd src
dotnet restore WoW.Two.Sdk.Backend.Beta.slnx -m:1
MSBUILDDISABLENODEREUSE=1 dotnet build WoW.Two.Sdk.Backend.Beta.slnx --no-restore -m:1
```

Each concern keeps its own folder lead doc `{folder}.md` / `*.spec.md` / `*.standard.md` in its folder as documentation (lead doc is `{folder}.md`, never a sub-folder `README.md`).
