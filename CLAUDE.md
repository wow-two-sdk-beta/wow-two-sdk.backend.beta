# wow-two-sdk.backend.beta

## What is this

The `WoW.Two.Sdk.Backend.Beta.*` family — beta-forever .NET 9 backend SDK aggregating wrappers around the entire .NET ecosystem (~10K+ packages reachable via composition). Single big NuGet meta with subpath imports per concern. Same beta-forever philosophy as the UI lib: no CHANGELOG, no PR gates, no required tests, push to main, fix-forward.

> **Beta-forever rules**: no CHANGELOG, no PR gates, no required tests, push directly to main, fix-forward when broken. CI builds + auto-bumps `0.0.y` on each main push.

> **⚠️ Structure (current): MONO-LIB.** The per-concern `.csproj` files were collapsed into **two** class libraries — `src/WoW.Two.Sdk.Backend.Beta.csproj` (all shipping concerns) and `src/testing/WoW.Two.Sdk.Backend.Beta.Testing.csproj` (test helpers). Each globs its folder tree; the per-area **folders and namespaces are unchanged**. Rationale + migration log + backlog: [`docs/analysis/mono-lib-migration.md`](./docs/analysis/mono-lib-migration.md). We split back into granular packages when the surface matures. Sections below that describe "per-package csproj" shape are historical until rewritten.

## Source-of-truth docs

- **[`docs/analysis/philosophy/ideas.md`](./docs/analysis/philosophy/ideas.md)** — encyclopedic catalog of every .NET tech / pattern / library / runtime API. **No verdicts** — pure inventory. Source of ideas; read when considering scope expansion.
- **[`docs/analysis/philosophy/targets.md`](./docs/analysis/philosophy/targets.md)** — verdict per item: **DONE / NOW / NEXT / LATER / MAYBE / SKIP / LOCKED**. Mirrors `ideas.md`'s structure. Read when deciding what to ship next.
- **[`docs/conventions/`](./docs/conventions/)** — package layout, naming, documentation strategy, package registry (lookup table).
- **[`docs/templates/`](./docs/templates/)** — copy-paste templates for new packages (`csproj`, `Module.cs`, `Options.cs`, `standard.md`, `spec.md`, `Tests.cs`, `folder-doc.md` — the folder lead doc, copied to `{folder}.md`).
- **[`docs/planning/platform-planning.md`](./docs/planning/platform-planning.md)** — standing roadmap + backlog of lego features to build; deep-dives under `docs/planning/<feature>/` (e.g. [`identity/identity-architecture.md`](./docs/planning/identity/identity-architecture.md)). Format follows `conventions/planning/platform-planning/`.

When scope expansion is considered, walk `targets.md` first. If the desired vector is missing or marked **MAYBE/LATER**, raise it for triage and update both files. Treat these two as a paired source-of-truth — when one changes, sync the other.

## Phase model (P0–P6)

See [`targets.md` §6](./docs/analysis/philosophy/targets.md#6-phase-mapping). Quick reference:

| Phase | Bundle | Status |
|---|---|---|
| P0 | testing scaffold (parallel track) | ✅ shipped (12 packages) |
| P1 | boot floor — foundation + observability + web basics | ✅ shipped (24 packages: 7 foundation + 8 observability + 9 web) |
| P2 | request pipeline + auth | ✅ shipped (mediator + identity, incl. otp/otp.telegram/jwt.issuance/policies + 16 OAuth providers) |
| P3 | persistence + outbound | 🚧 data ✅ · http ✅ · caching deferred |
| P4 | distributed essentials | 🚧 comms/email ✅ · jobs/hangfire(+postgres) ✅ · messaging + webhooks planned |
| meta | `AddApiDefaults()` / `UseApiDefaults()` one-import boot floor (`src/meta/`, root namespace) | ✅ shipped |
| P5 | SaaS-shaped (tenancy + AI + flags) | planned |
| P6 | heavy domain extensions | planned |

## Repo layout

```
wow-two-sdk.backend.beta/
├── src/                    ← all csprojs + sln + Directory.{Build,Packages}.props + .editorconfig
│   ├── meta/                          ← WoW.Two.Sdk.Backend.Beta meta-package
│   ├── foundation/                    ← P1 leaf packages (time, errors, results, validation, ...)
│   ├── observability/                 ← P1
│   ├── web/                           ← P1
│   ├── mediator/                      ← P2
│   ├── identity/                      ← P2
│   ├── data/                          ← P3
│   ├── caching/                       ← P3
│   ├── http/                          ← P3
│   ├── messaging/                     ← P4
│   ├── jobs/                          ← P4
│   ├── comms/                         ← P4
│   ├── tenancy/                       ← P5
│   ├── ai/                            ← P5
│   ├── feature-flags/                 ← P5
│   ├── realtime/                      ← P6
│   ├── storage/                       ← P6
│   ├── search/                        ← P6
│   ├── workflow/                      ← P6
│   └── testing/                       ← P0 (parallel track)
├── docs/
│   ├── analysis/philosophy/           ← ideas.md + targets.md (source-of-truth)
│   ├── conventions/                   ← layout, naming, doc-strategy, package-registry
│   └── templates/                     ← reusable per-package templates
└── apps/playground/                   ← (planned) Aspire AppHost end-to-end smoke
```

## Per-package shape

Every wrapper folder follows:

```
src/<area>/<package>/
├── WoW.Two.Sdk.Backend.Beta.<Domain>.csproj
├── <Module>ServiceCollectionExtensions.cs   ← descriptive `Add<Concrete>` extension(s)
├── <Public types>.cs
├── <Module>.standard.md                     ← RFC 2119 contract (when API has shape)
├── <Module>.spec.md                         ← API + usage snippets (when API has shape)
└── <folder>.md                              ← folder lead doc (kebab folder name, e.g. `time.md`) — 1-screen quickstart + see-also. NOT `README.md` below the repo root
```

**Naming**: package id carries the brand (`WoW.Two.Sdk.Backend.Beta.<Area>`), but **method/class names do NOT have a `WowTwo` prefix** — they describe what they actually do (e.g. `AddJwtBearerAuthentication`, `AddOpenTelemetryTracing`, `UseOwaspSecureHeaders`, `JsonOptionsPresets`, `WebApiTestBase<T>`). Mirrors the older `Backbone.Language.Features.Serialization` package convention. See [`docs/conventions/naming.md`](./docs/conventions/naming.md).

Tiny adapter packages (e.g. each container engine) ship just csproj + main file + the folder lead doc `{folder}.md`. Standard/spec are reserved for packages where the API has non-trivial shape.

See [`docs/conventions/package-layout.md`](./docs/conventions/package-layout.md).

## Dependency model

- `Directory.Packages.props` is the single source of truth for NuGet versions (Central Package Management).
- Every csproj inherits `Directory.Build.props` — `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `Nullable enable`, `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, `MIT`, source-link, deterministic builds.
- Foundation can NOT import domain packages. Domain packages can import any sibling domain. (Future: enforce via Roslyn analyzer.)
- Solution file is `src/WoW.Two.Sdk.Backend.Beta.slnx` (.NET 10 SDK XML solution format).

## Build & test

```bash
cd src
dotnet restore -m:1                                  # -m:1 avoids EMFILE on macOS
dotnet build WoW.Two.Sdk.Backend.Beta.slnx --no-restore -m:1
```

Set `MSBUILDDISABLENODEREUSE=1` and `ulimit -n 65535` if you hit "too many open files."

## Package naming

`WoW.Two.Sdk.Backend.Beta.<Area>[.<SubArea>]` — see [`docs/conventions/naming.md`](./docs/conventions/naming.md).

Registration: descriptive method names without `WowTwo` prefix — `services.AddJwtBearerAuthentication(...)`, `services.AddPerIpSlidingWindowRateLimit()`, `services.AddOpenTelemetryTracing(...)`. The package name carries the brand; the method name carries the meaning.

## Documentation strategy

**Wrappers** ship docs (`spec.md`, `standard.md`, the folder lead doc `{folder}.md`, `Tests.cs` examples). **Underlying libs** are NOT documented by us — we link to their official docs.

See [`docs/conventions/documentation.md`](./docs/conventions/documentation.md).

## Working rules

- **Spec before code** for any wrapper with non-trivial API shape.
- **Foundation cannot import domain packages.** Future Roslyn analyzer.
- **Source-gen first** — Mapperly, Vogen, source-gen JSON, source-gen logging, source-gen options validation.
- **Every public type has XML doc.** Enforced via `GenerateDocumentationFile` + analyzer.
- **No commercial-license dependencies in the core meta-package.** MediatR/AutoMapper/MassTransit/Duende/iText are all SKIP'd from core; opt-in adapters in companion packages only.
- **Permissive licenses only in core**: MIT, Apache-2.0, BSD-3, BSD-2, MS-PL, Unlicense.

## Out of scope

- No tests of the SDK itself (beta-forever rule)
- No CHANGELOG (git log is the changelog)
- No PR review (push to main)
- No graduation/distill rule yet — beta-forever until platform layer matures.
