using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>
/// Drift: editing an already-applied Apply.sql changes its checksum, so the next apply fails closed
/// (<see cref="MigrationDriftException"/>). Repair (needs <c>AllowRollback</c>) re-records and clears the drift.
/// </summary>
public sealed class DriftTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task ApplyPending_AfterAppliedSourceEdited_ThrowsDrift_AndRepairClearsIt()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");

        await using var migrator = CreateMigrator(o => o.AllowRollback = true);
        await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);

        // Edit the applied migration's source on disk → its checksum no longer matches the recorded one.
        Workspace.OverwriteApply("001-baseline", "create table t1(id int primary key, extra text);");

        // Status surfaces the drift without throwing.
        var drifted = await migrator.Runner.GetStatusAsync(CancellationToken.None);
        drifted.Drifted.Select(d => d.Label).Should().Equal("001-baseline");

        // Apply fails closed on drift, naming the drifted label.
        var ex = await Assert.ThrowsAsync<MigrationDriftException>(
            () => migrator.Runner.ApplyPendingAsync("test", CancellationToken.None));
        ex.Drifted.Should().Contain("001-baseline");

        // Repair re-records the stored checksum to match the (edited) source.
        await migrator.Runner.RepairAsync(CancellationToken.None);

        // Drift is gone; nothing pending; apply is a clean no-op again.
        var after = await migrator.Runner.GetStatusAsync(CancellationToken.None);
        after.Drifted.Should().BeEmpty();
        after.Pending.Should().BeEmpty();
        (await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Repair_WithoutAllowRollback_Throws()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");

        await using var migrator = CreateMigrator(); // AllowRollback defaults to false
        await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => migrator.Runner.RepairAsync(CancellationToken.None));
    }
}
