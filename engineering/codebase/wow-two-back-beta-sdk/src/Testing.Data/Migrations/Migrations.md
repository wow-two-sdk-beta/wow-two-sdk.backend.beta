# Testing.Data — Migrations

Test helpers for the SDK bespoke SQL migrator. Reference `WoW2.Sdk.Backend.Beta.Testing.Data` from a test project.

- `MigratorHarness` — the DI-graph-under-test: `CreatePostgres` / `CreateSqlite` (filesystem root or embedded assembly) + `Runner`, `Options`, `ReadHistoryAsync`, `HasTableAsync`, `HasIndexAsync`, `OpenConnectionAsync`.
- `MigratorPostgresFixture` — a Postgres container whose `ResetAsync` drops + recreates `public` (wiping `migration_history`) so each test applies from scratch — the opposite of base `PostgresFixture` (Respawn, preserves history). Serves directly as an `ICollectionFixture<>`.
- `MigratorTestBase` — drop-per-test convenience: resets the shared fixture + owns a fresh `MigrationsWorkspace`.
- `MigrationsWorkspace` — throwaway on-disk `{root}/NNN-name/{Apply,Rollback}.sql` root.
- `MigrationHistoryRow` — projected `migration_history` row for assertions.
- `DisableBespokeMigrator()` — replaces the dialect + runner with no-ops so a startup migrate hook touches nothing (unit tiers whose schema comes from EF `EnsureCreated`).

## Which Postgres fixture

Migrator suite (apply-from-scratch / drift / rollback) → `MigratorPostgresFixture`. E2E suite → base `Testing`'s `PostgresFixture`.

## Example

```csharp
[CollectionDefinition(Name)]
public sealed class MigratorCollection : ICollectionFixture<MigratorPostgresFixture>
{
    public const string Name = "app-migrator";
}

[Collection(MigratorCollection.Name)]
public sealed class ApplyTests(MigratorPostgresFixture fixture) : MigratorTestBase(fixture)
{
    [Fact]
    public async Task Apply_creates_table()
    {
        Workspace.Write("001-baseline", "create table t1(id int primary key);", "drop table t1;");
        await using var migrator = CreateMigrator();
        await migrator.Runner.ApplyPendingAsync("test");
        (await migrator.HasTableAsync("t1")).ShouldBeTrue();
    }
}
```
