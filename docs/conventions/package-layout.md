# Package layout

*Last updated: 2026-05-04*

## Repo structure

```
wow-two-sdk.backend.beta/
├── src/
│   ├── WoW.Two.Sdk.Backend.Beta.slnx      ← solution at src/ root (.slnx — canonical)
│   ├── WoW.Two.Sdk.Backend.Beta.csproj    ← core lib (globs every shipping folder)
│   ├── Directory.Build.props              ← shared MSBuild properties
│   ├── Directory.Packages.props           ← centralized package versions
│   ├── .editorconfig                      ← rules for entire src/
│   │
│   ├── Meta/
│   │   └── WoW.Two.Sdk.Backend.Beta/      ← meta-package — refs curated set
│   │
│   ├── Foundation/                        ← P1 — leaf-level deps
│   │   ├── Time/         → WoW.Two.Sdk.Backend.Beta.Foundation.Time
│   │   ├── Errors/       → WoW.Two.Sdk.Backend.Beta.Foundation.Errors
│   │   ├── Results/      → WoW.Two.Sdk.Backend.Beta.Foundation.Results
│   │   ├── Validation/   → WoW.Two.Sdk.Backend.Beta.Foundation.Validation
│   │   └── Serialization/ → WoW.Two.Sdk.Backend.Beta.Foundation.Serialization
│   │
│   ├── Observability/                     ← P1
│   │   ├── Logging/
│   │   ├── Tracing/
│   │   ├── Metrics/
│   │   └── HealthChecks/
│   │
│   ├── Web/                               ← P1
│   │   ├── Hosting/
│   │   ├── OpenApi/
│   │   ├── ProblemDetails/
│   │   ├── RateLimit/
│   │   ├── OutputCache/
│   │   ├── SecureHeaders/
│   │   └── Cors/
│   │
│   ├── Mediator/                          ← P2
│   ├── Identity/                          ← P2
│   ├── Data/                              ← P3
│   ├── Caching/                           ← P3
│   ├── Http/                              ← P3
│   ├── Messaging/                         ← P4
│   ├── Jobs/                              ← P4
│   ├── Comms/                             ← P4
│   ├── Tenancy/                           ← P5
│   ├── Ai/                                ← P5
│   ├── FeatureFlags/                      ← P5
│   ├── Realtime/                          ← P6
│   ├── Storage/                           ← P6
│   ├── Search/                            ← P6
│   ├── Workflow/                          ← P6
│   │
│   └── Testing/                           ← P0 — parallel, ships continuously
│       ├── Assertions/
│       ├── Containers/
│       ├── Verify/
│       ├── Bogus/
│       └── WireMock/
│
├── docs/
│   ├── analysis/philosophy/               ← ideas.md + targets.md
│   ├── conventions/                       ← this folder
│   └── templates/                         ← reusable templates
│
├── apps/
│   └── playground/                        ← Aspire AppHost end-to-end demo
│
├── README.md
└── CLAUDE.md (TBD)
```

## Per-package folder shape

```
src/<area>/<package>/
├── WoW.Two.Sdk.Backend.Beta.<Domain>.csproj
├── <Module>Module.cs                      ← `Add<Module>` IServiceCollection extension
├── <Public types>.cs                      ← API surface
├── Internal/                              ← anything `internal class`
├── <Module>.standard.md                   ← RFC 2119 behavioral contract
├── <Module>.spec.md                       ← API + usage snippets
├── <Module>.tests.cs                      ← runnable examples (xUnit)
└── <Folder>.md                            ← folder lead doc (PascalCase folder name, e.g. `Time.md`) — 1-screen quickstart. NOT `README.md` (only the repo root may be `README.md`)
```

> No `tests/` separate folder. Test files live next to the wrapper they document.
> Folder lead doc is `{Folder}.md` (PascalCase, matching the folder), never a per-folder `README.md` — see `repo-structure.md` §3.

## Naming

- **NuGet package id**: `WoW.Two.Sdk.Backend.Beta.<Domain>` (PascalCase, dotted)
- **Folder name**: `PascalCase`, matching the package-id/namespace segment 1:1 (`Time/`, `OutputCache/`, `FeatureFlags/`)
- **Repo namespace**: `WoW.Two.Sdk.Backend.Beta.<Domain>` matches package id
- **Public extension class**: `<Domain>ServiceCollectionExtensions` (XxxServiceCollectionExtensions pattern Microsoft uses)
- **Public extension method**: `Add<Domain>(this IServiceCollection services, …)` returning `IServiceCollection`
- **Public options type**: `<Domain>Options` (record with init-only properties)

## Layering rule

**Foundation can not import domain packages.** Foundation = `time`, `errors`, `results`, `validation`, `serialization`. Domain = everything else.

Domain packages can reference any sibling domain. Convention: keep dependencies one-way where natural; cycles never.

This mirrors the UI lib's foundation/domain rule. Future: enforce via custom Roslyn analyzer.

## Per-package file conventions

- Use **file-scoped namespaces** (`namespace WoW.Two.Sdk.Backend.Beta.Time;`)
- Use **C# 13+** features freely (primary constructors, collection expressions, partial properties)
- **Nullable enabled** + warnings as errors for nullability
- **`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`** on every csproj
- **Every public type has XML doc comment** (enforced via analyzer)
- **No `internal sealed class _<Helper>`** style — prefer `internal sealed class XxxHelper`

## Solution organization

Single `WoW.Two.Sdk.Backend.Beta.slnx` at `src/` root, with **solution folders** mirroring the directory structure (Foundation, Observability, Web, etc.).

For consumers and IDE perf at scale, plan to add `.slnf` filters per phase (e.g., `WoW.Two.Sdk.Backend.Beta.P1.slnf` opens just P1 packages).

## Versioning

- All packages ship as `0.x.y`
- Single version across all packages — bumped together by CI on push to `main`
- Consumers should pin exact versions for stability
- See `docs/conventions/versioning.md` (TBD)

## Build / pack

Each csproj is configured to produce a NuGet on `dotnet pack`. CI publishes the entire bundle in lockstep.

## What does NOT live in src/

- Tests of the SDK itself — beta-forever rule says no required tests for the lib
- Sample apps for individual packages — only `apps/playground/` end-to-end
- Documentation generators — `docs/` is hand-authored markdown
