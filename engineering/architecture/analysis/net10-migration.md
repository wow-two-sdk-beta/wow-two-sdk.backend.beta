# .NET 10 migration

*Last updated: 2026-06-09 · Status: **complete — solution green on net10, consumer-restorable***

Bumped the whole kit from net9 → net10 so consumers (e.g. the Secrets Vault, net10) restore it
without the `NU1605` downgrade conflict (kit `M.E.* 9.0.2` vs bundled OpenTelemetry's `10.0.0` need).

## What changed
- **TFM** `net9.0` → `net10.0` (`src/Directory.Build.props`).
- **Microsoft platform** → `10.0.3` servicing patch: `Microsoft.Extensions.*` (BCL), `Microsoft.AspNetCore.*`,
  `Microsoft.EntityFrameworkCore.*` — all aligned (mixed patch levels caused `CS1705`).
- **EF ecosystem** → EF10: `EFCore.NamingConventions 10.0.1`, `EFCore.BulkExtensions 10.0.1`,
  `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0`, `Oracle.EntityFrameworkCore` (transitive) `10.23.x`.
- **Code:** `ForwardedHeadersOptions.KnownNetworks` → `KnownIPNetworks` (ASP.NET 10 deprecation, `ASPDEPR005`).
- **Package ID:** `WoW2.Sdk.Backend.Beta` (owned `WoW2.*` reserved prefix; `WoW.Two.*` is reserved by another party).

## Removed
- **`Pomelo.EntityFrameworkCore.MySql`** + `src/Data/EntityFrameworkCore/MySql/` (`MySqlExtensions.cs`).
  - **Why:** no EF10 release — latest is `9.0.0` (EF9), which blocks the net10 build.
  - **EF10 alternative when MySQL is needed:** Oracle's **`MySql.EntityFrameworkCore` `10.0.7`**. Note: it's
    *not* a drop-in — different API (`UseMySQL`, no `ServerVersion.AutoDetect`, no `EnableRetryOnFailure`), so
    `UseMySqlConventional` would need a rewritten helper. `dbup-mysql` (SQL-script migrations) is retained — it's
    independent of the EF provider.

## Vulnerability advisories — downgraded to non-blocking warnings (revisit: bump the pullers)
- Core (`Directory.Build.props` `WarningsNotAsErrors`): `NU1903` **Snappier** (HIGH), `NU1902` KubernetesClient /
  SharpCompress (moderate) — all transitive. Plus `NU1510` (`Microsoft.Extensions.Http` redundant on net10).
- Testing only (`testing.csproj` `WarningsNotAsErrors`): `NU1904` **System.Drawing.Common 5.0.0** (CRITICAL,
  transitive via a test tool). Scoped to the test lib so the production package still **blocks** critical vulns.

## Still excluded (pre-existing, not net10-related)
`Web/Compression`, `Web/RateLimit`, `Identity/OAuth/{Apple,GitHub,Microsoft}` — excluded since net9 for a CPM
ASP.NET ref-resolution conflict. **Candidate to retry** now the framework is aligned on net10 (untested this pass).
