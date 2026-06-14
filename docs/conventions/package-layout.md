# Package layout

*Last updated: 2026-05-04*

## Repo structure

```
wow-two-sdk.backend.beta/
├── src/
│   ├── WoW.Two.Sdk.Backend.Beta.sln       ← solution at src/ root
│   ├── Directory.Build.props              ← shared MSBuild properties
│   ├── Directory.Packages.props           ← centralized package versions
│   ├── .editorconfig                      ← rules for entire src/
│   │
│   ├── meta/
│   │   └── WoW.Two.Sdk.Backend.Beta/      ← meta-package — refs curated set
│   │
│   ├── foundation/                        ← P1 — leaf-level deps
│   │   ├── time/         → WoW.Two.Sdk.Backend.Beta.Time
│   │   ├── errors/       → WoW.Two.Sdk.Backend.Beta.Errors
│   │   ├── results/      → WoW.Two.Sdk.Backend.Beta.Results
│   │   ├── validation/   → WoW.Two.Sdk.Backend.Beta.Validation
│   │   └── serialization/ → WoW.Two.Sdk.Backend.Beta.Serialization
│   │
│   ├── observability/                     ← P1
│   │   ├── logging/
│   │   ├── tracing/
│   │   ├── metrics/
│   │   └── healthchecks/
│   │
│   ├── web/                               ← P1
│   │   ├── hosting/
│   │   ├── openapi/
│   │   ├── problemdetails/
│   │   ├── ratelimit/
│   │   ├── outputcache/
│   │   ├── secureheaders/
│   │   └── cors/
│   │
│   ├── mediator/                          ← P2
│   ├── identity/                          ← P2
│   ├── data/                              ← P3
│   ├── caching/                           ← P3
│   ├── http/                              ← P3
│   ├── messaging/                         ← P4
│   ├── jobs/                              ← P4
│   ├── comms/                             ← P4
│   ├── tenancy/                           ← P5
│   ├── ai/                                ← P5
│   ├── feature-flags/                     ← P5
│   ├── realtime/                          ← P6
│   ├── storage/                           ← P6
│   ├── search/                            ← P6
│   ├── workflow/                          ← P6
│   │
│   └── testing/                           ← P0 — parallel, ships continuously
│       ├── core/
│       ├── containers/
│       ├── verify/
│       ├── bogus/
│       ├── wiremock/
│       └── assertions/
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
└── <folder>.md                            ← folder lead doc (kebab folder name, e.g. `time.md`) — 1-screen quickstart. NOT `README.md` (only the repo root may be `README.md`)
```

> No `tests/` separate folder. Test files live next to the wrapper they document.
> Folder lead doc is `{folder}.md` (kebab-cased), never a per-folder `README.md` — see `repo-structure.md` §3.

## Naming

- **NuGet package id**: `WoW.Two.Sdk.Backend.Beta.<Domain>` (PascalCase, dotted)
- **Folder name**: `kebab-case` (`time/`, `output-cache/`, `feature-flags/`)
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

Single `WoW.Two.Sdk.Backend.Beta.sln` at `src/` root, with **solution folders** mirroring the directory structure (foundation, observability, web, etc.).

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
