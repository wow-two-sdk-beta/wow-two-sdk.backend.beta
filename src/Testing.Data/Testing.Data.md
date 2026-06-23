# Testing.Data

Data-tier test helpers for the backend SDK — the companion to the dependency-light `Testing` package.

Holds the helpers that need the core mono-lib (migrator engine, connection factories) or EF Core, so the base `Testing` package never takes that dependency. Reference `WoW2.Sdk.Backend.Beta.Testing.Data` from a test project only; it pulls base `Testing` + core transitively.

## Layout

- `Migrations/` — `MigratorHarness` (the DI-graph-under-test + history/table/index assertions, Postgres + SQLite, filesystem + embedded sources), `MigratorPostgresFixture` (drop-schema reset for migrator suites — the opposite of base `PostgresFixture`'s Respawn reset), `MigratorTestBase` (drop-per-test filesystem convenience), `MigrationsWorkspace`, `MigrationHistoryRow`, and `DisableBespokeMigrator()` (no-op migrator for non-Docker tiers).
- `EntityFrameworkCore/` — `RemoveAllForDbContext<T>` + `RepointDbContext<T>` to swap an app's EF provider onto a test provider.

## Which Postgres fixture

- Migrator suites (apply-from-scratch / drift / rollback) → `MigratorPostgresFixture` — `ResetAsync` drops + recreates `public`, wiping `migration_history`.
- E2E suites → base `Testing`'s `PostgresFixture` — Respawn truncates data but preserves `migration_history`.
