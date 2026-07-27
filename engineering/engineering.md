# engineering

*Last updated: 2026-07-27*

The doc-bearing shell for `WoW.Two.Sdk.Backend.Beta` — everything except the shipped NuGet mono-lib itself.

| Dir | Holds |
|---|---|
| [`planning/`](./planning/) | Roadmap + backlog ([`platform-planning.md`](./planning/platform-planning.md)) · per-vector deep-dives: [`data-pipeline/`](./planning/data-pipeline/) (+ `research/`) · [`errors/`](./planning/errors/) · [`identity/`](./planning/identity/) · [`mediator-cqrs/`](./planning/mediator-cqrs/) · [`messaging/`](./planning/messaging/) · [`component-registry/`](./planning/component-registry/) |
| [`architecture/`](./architecture/) | [`package-layout.md`](./architecture/package-layout.md) (repo + package shape, layering, package-id grammar, doc strategy) · [`package-registry.md`](./architecture/package-registry.md) (per-area shipped status) · `templates/` (new-package templates) · `analysis/` (design records + [`VECTOR-ANALYSIS.md`](./architecture/analysis/VECTOR-ANALYSIS.md)) · `analysis/philosophy/` ([`ideas.md`](./architecture/analysis/philosophy/ideas.md) + [`targets.md`](./architecture/analysis/philosophy/targets.md) — paired source-of-truth) |
| [`codebase/`](./codebase/) | The NuGet package → [`wow-two-back-beta-sdk/`](./codebase/wow-two-back-beta-sdk/) |

A bare `src/…` in any doc is **code-dir-relative** — `codebase/wow-two-back-beta-sdk/src/…`.

Layout follows [`sdk-structure.md`](../../../../conventions/development/repo/structure/sdk-structure.md). Package conventions live in the repo-root [`CLAUDE.md`](../CLAUDE.md).
