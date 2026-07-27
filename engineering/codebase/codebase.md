# codebase

*Last updated: 2026-07-27*

One NuGet mono-lib, one code dir (per [`sdk-structure.md`](../../../../../conventions/development/repo/structure/sdk-structure.md)).

- [`wow-two-back-beta-sdk/`](./wow-two-back-beta-sdk/) — the `WoW.Two.Sdk.Backend.Beta` package: `src/` holds every csproj, `WoW.Two.Sdk.Backend.Beta.slnx`, `Directory.{Build,Packages}.props`, and `.editorconfig`.

| Run from | Command |
|---|---|
| `wow-two-back-beta-sdk/src` | `dotnet build WoW.Two.Sdk.Backend.Beta.slnx -m:1` (`-m:1` + `MSBUILDDISABLENODEREUSE=1` + `ulimit -n 65535` avoid EMFILE on macOS) |
| repo root (CI) | `publish.yml` sets `defaults.run.working-directory: engineering/codebase/wow-two-back-beta-sdk` — every `src/…` path in the workflow is code-dir-relative |

Package conventions + layering rules: repo-root [`CLAUDE.md`](../../CLAUDE.md) · shape: [`../architecture/package-layout.md`](../architecture/package-layout.md).
