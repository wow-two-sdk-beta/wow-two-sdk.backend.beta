# wow-two-sdk.backend.beta

## What is this

The `WoW.Two.Sdk.Backend.Beta.*` family — beta-forever .NET 9 backend SDK aggregating wrappers around the entire .NET ecosystem (~10K+ packages reachable via composition). Single big NuGet meta with subpath imports per concern. Same beta-forever philosophy as the UI lib: no CHANGELOG, no PR gates, no required tests, push to main, fix-forward.

> **Beta-forever rules**: no CHANGELOG, no PR gates, no required tests, push directly to main, fix-forward when broken. CI builds + auto-bumps `0.0.y` on each main push.

> **⚠️ Structure (current): MONO-LIB.** The per-concern `.csproj` files were collapsed into **two** class libraries — `src/WoW.Two.Sdk.Backend.Beta.csproj` (all shipping concerns) and `src/Testing/WoW.Two.Sdk.Backend.Beta.Testing.csproj` (test helpers). Each globs its folder tree; the per-area folders are **PascalCase, matching the namespace/package-id segment 1:1**, with provider leaves nested under their concept parent (e.g. `src/Comms/Email/MailKit/` → `…Comms.Email.MailKit`). Rationale + migration log + backlog: [`engineering/architecture/analysis/mono-lib-migration.md`](./engineering/architecture/analysis/mono-lib-migration.md). We split back into granular packages when the surface matures. Sections below that describe "per-package csproj" shape are historical until rewritten.

## Source-of-truth docs

- **[`engineering/architecture/analysis/philosophy/ideas.md`](./engineering/architecture/analysis/philosophy/ideas.md)** — encyclopedic catalog of every .NET tech / pattern / library / runtime API. **No verdicts** — pure inventory. Source of ideas; read when considering scope expansion.
- **[`engineering/architecture/analysis/philosophy/targets.md`](./engineering/architecture/analysis/philosophy/targets.md)** — verdict per item: **DONE / NOW / NEXT / LATER / MAYBE / SKIP / LOCKED**. Mirrors `ideas.md`'s structure. Read when deciding what to ship next.
- **[`engineering/architecture/package-layout.md`](./engineering/architecture/package-layout.md)** — SDK-internal architecture: repo + per-package shape, layering, package-id grammar, three-layer doc strategy. Code-style conventions (naming, documentation) are centralized in `wow-two-ws/conventions/`.
- **[`engineering/architecture/package-registry.md`](./engineering/architecture/package-registry.md)** — per-area shipped-status lookup table.
- **[`engineering/architecture/templates/`](./engineering/architecture/templates/)** — copy-paste templates for new packages (`csproj`, `Module.cs`, `Options.cs`, `standard.md`, `spec.md`, `Tests.cs`, `folder-doc.md` — the folder lead doc, copied to `{folder}.md`).
- **[`engineering/planning/platform-planning.md`](./engineering/planning/platform-planning.md)** — standing roadmap + backlog of lego features to build; deep-dives under `engineering/planning/<feature>/` (e.g. [`identity/identity-architecture.md`](./engineering/planning/identity/identity-architecture.md)). Format follows `conventions/planning/platform-planning/`.

When scope expansion is considered, walk `targets.md` first. If the desired vector is missing or marked **MAYBE/LATER**, raise it for triage and update both files. Treat these two as a paired source-of-truth — when one changes, sync the other.

## Phase model (P0–P6)

See [`targets.md` §6](./engineering/architecture/analysis/philosophy/targets.md#6-phase-mapping). Quick reference:

| Phase | Bundle | Status |
|---|---|---|
| P0 | testing scaffold (parallel track) | ✅ shipped (12 packages) |
| P1 | boot floor — foundation + observability + web basics | ✅ shipped (24 packages: 7 foundation + 8 observability + 9 web) |
| P2 | request pipeline + auth | ✅ shipped (mediator + identity, incl. otp/otp.telegram/jwt.issuance/policies + 16 OAuth providers) |
| P3 | persistence + outbound | 🚧 data ✅ · http ✅ · caching deferred |
| P4 | distributed essentials | 🚧 comms/email ✅ · jobs/hangfire(+postgres) ✅ · messaging + webhooks planned |
| meta | `AddApiDefaults()` / `UseApiDefaults()` one-import boot floor (`src/Meta/`) | ✅ shipped |
| P5 | SaaS-shaped (tenancy + AI + flags) | planned |
| P6 | heavy domain extensions | planned |

## Repo layout

Shell shape per [`sdk-structure.md`](../../../conventions/development/repo/structure/sdk-structure.md) — `engineering/` bears the docs, `engineering/codebase/{slug}/` bears the one code dir.

```
wow-two-sdk.backend.beta/
├── README.md · CLAUDE.md · .github/workflows/  ← entry docs + CI (root-pinned)
└── engineering/
    ├── engineering.md
    ├── planning/                       ← platform-planning.md + per-vector deep-dives
    │                                     (data-pipeline · errors · identity · mediator-cqrs · messaging · component-registry)
    ├── architecture/
    │   ├── package-layout.md           ← SDK-internal architecture + doc strategy
    │   ├── package-registry.md         ← per-area shipped-status lookup table
    │   ├── templates/                  ← reusable per-package templates
    │   └── analysis/                   ← VECTOR-ANALYSIS.md · mono-lib-migration · net10-migration · …
    │       └── philosophy/             ← ideas.md + targets.md (source-of-truth)
    └── codebase/
        ├── codebase.md
        └── wow-two-back-beta-sdk/      ← THE code dir — all `src/…` paths below are relative to here
            └── src/                    ← all csprojs + slnx + Directory.{Build,Packages}.props + .editorconfig
                ├── Meta/                       ← WoW.Two.Sdk.Backend.Beta meta-package
                ├── Foundation/                 ← P1 leaves (Time, Errors, Results, Validation, Naming, …)
                ├── Observability/ Web/         ← P1
                ├── Mediator/ Identity/         ← P2
                ├── Data/ Caching/ Http/        ← P3
                ├── Messaging/ Jobs/ Comms/     ← P4
                ├── Tenancy/ Ai/ FeatureFlags/  ← P5
                ├── Realtime/ Storage/          ← P6
                ├── Codes/ Geo/ Localization/ Media/ Integrations/
                ├── Testing/ Testing.Data/ Testing.Integrations/ Testing.Messaging/   ← P0 (parallel track)
                └── *.Tests/                    ← Foundation · Identity · Mediator · Messaging · Migrations · Web
```

> **Path convention**: a bare `src/…` in any doc is **code-dir-relative** (`engineering/codebase/wow-two-back-beta-sdk/src/…`), matching the frontend SDK.

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

**Naming**: package id carries the brand (`WoW.Two.Sdk.Backend.Beta.<Area>`), but **method/class names do NOT have a `WowTwo` prefix** — they describe what they actually do (e.g. `AddJwtBearerAuthentication`, `AddOpenTelemetryTracing`, `UseOwaspSecureHeaders`, `JsonOptionsPresets`, `WebApiTestBase<T>`). Mirrors the older `Backbone.Language.Features.Serialization` package convention. Symbol-level naming is centralized in `wow-two-ws/conventions/development/backend/code-style/naming.md` (§Registration and extension-method naming); package-id grammar lives in [`engineering/architecture/package-layout.md`](./engineering/architecture/package-layout.md).

Tiny adapter packages (e.g. each container engine) ship just csproj + main file + the folder lead doc `{folder}.md`. Standard/spec are reserved for packages where the API has non-trivial shape.

See [`engineering/architecture/package-layout.md`](./engineering/architecture/package-layout.md).

## Dependency model

- `Directory.Packages.props` is the single source of truth for NuGet versions (Central Package Management).
- Every csproj inherits `Directory.Build.props` — `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `Nullable enable`, `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, `MIT`, source-link, deterministic builds.
- Foundation can NOT import domain packages. Domain packages can import any sibling domain. (Future: enforce via Roslyn analyzer.)
- Solution file is `engineering/codebase/wow-two-back-beta-sdk/src/WoW.Two.Sdk.Backend.Beta.slnx` (.NET 10 SDK XML solution format).

## Build & test

```bash
cd engineering/codebase/wow-two-back-beta-sdk/src
dotnet restore -m:1                                  # -m:1 avoids EMFILE on macOS
dotnet build WoW.Two.Sdk.Backend.Beta.slnx --no-restore -m:1
```

Set `MSBUILDDISABLENODEREUSE=1` and `ulimit -n 65535` if you hit "too many open files."

## Package naming

`WoW.Two.Sdk.Backend.Beta.<Area>[.<SubArea>]` — package-id grammar in [`engineering/architecture/package-layout.md`](./engineering/architecture/package-layout.md).

Registration: descriptive method names without `WowTwo` prefix — `services.AddJwtBearerAuthentication(...)`, `services.AddPerIpSlidingWindowRateLimit()`, `services.AddOpenTelemetryTracing(...)`. The package name carries the brand; the method name carries the meaning. Full rule: `wow-two-ws/conventions/development/backend/code-style/naming.md` (§Registration and extension-method naming).

## Documentation strategy

**Wrappers** ship docs (`spec.md`, `standard.md`, the folder lead doc `{folder}.md`, `Tests.cs` examples). **Underlying libs** are NOT documented by us — we link to their official docs.

Three-layer strategy + cadence: [`engineering/architecture/package-layout.md`](./engineering/architecture/package-layout.md) §Doc strategy. Wrapper-doc format (`*.standard.md` / `*.spec.md` / xUnit-as-docs): `wow-two-ws/conventions/development/backend/code-style/documentation.md` §Wrapper / package docs.

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
