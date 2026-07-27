using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>Fresh-DB apply: pending → applied, history recorded, status reflects it, second apply is a no-op.</summary>
public sealed class ApplyTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task ApplyPending_ShouldApplyRecordAndBeIdempotent_WhenFreshDb()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");
        Workspace.Write("002-second",
            applySql: "create table t2(id int primary key);",
            rollbackSql: "drop table t2;");

        await using var migrator = CreateMigrator();

        // First apply: both pending migrations run.
        var applied = await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);
        applied.Should().BeEquivalentTo(["001-baseline", "002-second"]);

        // The tables they declare now exist.
        (await migrator.HasTableAsync("t1")).Should().BeTrue();
        (await migrator.HasTableAsync("t2")).Should().BeTrue();

        // migration_history has one row per migration, stamped with appliedBy (reading applied_at exercises the
        // SQLite DateTimeOffset type handler the dialect registers).
        var history = await migrator.ReadHistoryAsync();
        history.Select(r => r.Ordinal).Should().Equal(1, 2);
        history.Select(r => r.Name).Should().Equal("baseline", "second");
        history.Should().OnlyContain(r => r.AppliedBy == "test");

        // GetStatus shows both applied, nothing pending / drifted / orphaned.
        var status = await migrator.Runner.GetStatusAsync(CancellationToken.None);
        status.Applied.Select(a => a.Ordinal).Should().Equal(1, 2);
        status.Pending.Should().BeEmpty();
        status.Drifted.Should().BeEmpty();
        status.Orphaned.Should().BeEmpty();

        // Second apply against an up-to-date DB is a no-op: no labels, history unchanged.
        var second = await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);
        second.Should().BeEmpty();
        (await migrator.ReadHistoryAsync()).Should().HaveCount(2);
    }
}
