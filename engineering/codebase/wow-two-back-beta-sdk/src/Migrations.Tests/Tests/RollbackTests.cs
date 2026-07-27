using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>
/// Rollback: with <c>AllowRollback</c>, <c>RollbackAsync()</c> runs the latest migration's Rollback.sql and removes
/// its history row. With rollback disabled (the default) it throws.
/// </summary>
public sealed class RollbackTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task Rollback_ShouldRemoveLatestHistoryRowAndRunRollbackSql_WhenAllowRollback()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");
        Workspace.Write("002-second",
            applySql: "create table t2(id int primary key);",
            rollbackSql: "drop table t2;");

        await using var migrator = CreateMigrator(o => o.AllowRollback = true);
        await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);

        // Roll back the latest (002) only.
        await migrator.Runner.RollbackAsync(targetOrdinal: null, CancellationToken.None);

        // 002's Rollback.sql ran (t2 dropped) and its history row is gone; 001 untouched.
        (await migrator.HasTableAsync("t2")).Should().BeFalse();
        (await migrator.HasTableAsync("t1")).Should().BeTrue();
        (await migrator.ReadHistoryAsync()).Select(h => h.Ordinal).Should().Equal(1);

        // 002 is pending again — rollback returned it to the source-but-not-applied state.
        var status = await migrator.Runner.GetStatusAsync(CancellationToken.None);
        status.Applied.Select(a => a.Ordinal).Should().Equal(1);
        status.Pending.Select(p => p.Ordinal).Should().Equal(2);
    }

    [Fact]
    public async Task Rollback_ShouldThrow_WhenAllowRollbackDisabled()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");

        await using var migrator = CreateMigrator(); // AllowRollback defaults to false
        await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => migrator.Runner.RollbackAsync(targetOrdinal: null, CancellationToken.None));

        // The disabled rollback was a no-op: the migration is still applied.
        (await migrator.HasTableAsync("t1")).Should().BeTrue();
        (await migrator.ReadHistoryAsync()).Select(h => h.Ordinal).Should().Equal(1);
    }
}
