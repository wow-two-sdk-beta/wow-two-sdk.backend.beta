# Documentation strategy

*Last updated: 2026-05-04*

## Why this exists

We wrap the .NET ecosystem (~10K+ packages reachable via wrapper composition). We can't maintain runnable samples for every underlying lib — they change weekly. We can maintain documentation for **our wrappers**, because their cadence is our cadence.

## Three layers

### Layer 1 — wrapper code (always)

Every wrapper folder contains:

```
src/foundation/time/
├── WoW.Two.Sdk.Backend.Beta.Time.csproj
├── TimeModule.cs                    ← registration extension(s)
├── TimeProviderExtensions.cs        ← public API
├── TimeModule.standard.md           ← RFC 2119 contract — "must / should"
├── TimeModule.spec.md               ← concrete API + usage snippets
├── TimeModule.tests.cs              ← runnable examples (xUnit)
└── time.md                          ← 1-screen quickstart + see-also (folder lead doc = `{folder}.md`)
```

`*.standard.md` and `*.spec.md` borrow the UI lib's per-component pattern. `*.tests.cs` is our **Storybook for backend** — every wrapper test demonstrates intended usage AND catches regressions.

### Layer 2 — playground (one per repo)

`apps/playground/` — a single .NET Aspire AppHost that boots the meta-package end-to-end (DB, cache, messaging, identity, OTel dashboard). Mirrors UI lib's `apps/playground/`. Used for:

- onboarding ("clone, run, see everything wired")
- end-to-end smoke validation before a release tag
- live debugging when chasing a cross-package issue

Updated with each major release, not per-wrapper.

### Layer 3 — underlying-lib reference (lazy)

For underlying libs (EF Core, Polly, Serilog, etc.), we **don't** maintain showcase content. Instead:

- our wrapper's XML `<remarks>` links to the official lib doc
- our wrapper's `spec.md` has a `## See also` block pointing to canonical lib docs
- if we discover something subtle while wrapping, that knowledge lands in the existing `wow-two-kb` org (21 KB repos already exist for major .NET concerns) — never preemptively, only reactively

The IDEAS catalog (`docs/analysis/philosophy/ideas.md`) is the encyclopedic memory — not a maintenance commitment.

## Rules

- **Never write a sample app per underlying lib.** That's the 10K-packages trap.
- **Every wrapper ships with at least one xUnit test acting as docs.** Even trivial wrappers — the test proves the registration call works.
- **`*.standard.md` is RFC 2119 (MUST/SHOULD/MAY) only.** Behavior contract, not API. Survives API churn.
- **`*.spec.md` is concrete API + usage.** Matches code. Updated when wrapper API changes.
- **The folder lead doc `{folder}.md` is the 1-screen elevator pitch** (kebab-cased folder name, e.g. `time.md`, `output-cache.md`; never `README.md` below the repo root). Install, register, one snippet, see-also links.
- **XML doc comments on every public type.** `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is on globally.
- **Don't embed full underlying-lib API docs.** Link out instead.

## What we do *not* document

- Underlying lib internals
- Underlying lib version-by-version changelogs
- Migration guides between underlying-lib versions (consumers follow the underlying lib's own migration guide; our wrapper either absorbs the change silently or bumps its own version)
- Speculative usage scenarios — only ship docs for wrappers that exist

## Cadence rule

Documentation churn = **our** wrapper churn. If `wow-two-sdk.backend.beta.cache` doesn't change this month, none of its docs change either. The underlying `Microsoft.Extensions.Caching.Hybrid` could ship 12 patch releases — we don't care unless one of them breaks our wrapper, in which case we bump our wrapper and update its spec.

## Future / maybe

- DocFX-built docs site (LATER — phase P6)
- Auto-extracted API reference from XML doc comments (LATER)
- `*.usage.md` deprecated long-form usage essays (SKIP — keep it tight)
