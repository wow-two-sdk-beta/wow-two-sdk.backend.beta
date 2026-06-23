using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>
/// <c>-- @no-transaction</c>: the Apply runs outside a transaction and is recorded in a separate statement. SQLite
/// has no <c>CREATE INDEX CONCURRENTLY</c>, but the directive path is the same — an idempotent bare statement that
/// still applies + records, with a re-run being a no-op.
/// </summary>
public sealed class NoTransactionTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task ApplyPending_NoTransactionMigration_AppliesRecordsAndReRunIsNoOp()
    {
        // A transactional baseline that creates the table the bare index targets.
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key, name text);",
            rollbackSql: "drop table t1;");

        // The index Apply runs bare (no enclosing transaction) via the directive. IF NOT EXISTS keeps it idempotent,
        // which the no-transaction contract requires (a crash mid-apply re-runs it).
        Workspace.Write("002-bare-index",
            applySql: "-- @no-transaction\ncreate index if not exists ix_t1_name on t1(name);",
            rollbackSql: "drop index if exists ix_t1_name;");

        await using var migrator = CreateMigrator();

        var applied = await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);
        applied.Should().BeEquivalentTo(["001-baseline", "002-bare-index"]);

        // The index exists and the migration is recorded.
        (await migrator.HasIndexAsync("ix_t1_name")).Should().BeTrue();
        var history = await migrator.ReadHistoryAsync();
        history.Select(r => r.Ordinal).Should().Equal(1, 2);

        // Re-running the whole apply is a clean no-op (idempotent Apply + already-recorded row).
        var second = await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);
        second.Should().BeEmpty();
        (await migrator.ReadHistoryAsync()).Should().HaveCount(2);

        var status = await migrator.Runner.GetStatusAsync(CancellationToken.None);
        status.Pending.Should().BeEmpty();
        status.Drifted.Should().BeEmpty();
    }
}
