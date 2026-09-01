using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>
/// Drift: editing an already-applied Apply.sql changes its checksum, so the next apply fails closed
/// (a <c>DataIntegrity</c> failure). Repair (needs <c>AllowRollback</c>) re-records and clears the drift.
/// </summary>
public sealed class DriftTests : SqliteMigratorTestBase
{
    private static readonly string[] DriftedLabels = ["001-baseline"];

    [Fact]
    public async Task ApplyPending_ShouldThrowDriftAndRepairShouldClearIt_WhenAppliedSourceEdited()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");

        await using var migrator = CreateMigrator(o => o.AllowRollback = true);
        (await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None)).ValueOrThrow();

        // Edit the applied migration's source on disk → its checksum no longer matches the recorded one.
        Workspace.OverwriteApply("001-baseline", "create table t1(id int primary key, extra text);");

        // Status surfaces the drift without throwing.
        var drifted = (await migrator.Runner.GetStatusAsync(CancellationToken.None)).ValueOrThrow();
        drifted.Drifted.Select(d => d.Label).Should().Equal("001-baseline");

        // Apply fails closed on drift, naming the drifted label.
        var failure = await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);
        failure.IsFailure(out var driftError, out _).Should().BeTrue();
        driftError!.Type.Should().Be(AppErrorType.DataIntegrity);
        driftError.Metadata!["drifted"].Should().BeEquivalentTo(DriftedLabels);

        // Repair re-records the stored checksum to match the (edited) source.
        (await migrator.Runner.RepairAsync(CancellationToken.None)).ValueOrThrow();

        // Drift is gone; nothing pending; apply is a clean no-op again.
        var after = (await migrator.Runner.GetStatusAsync(CancellationToken.None)).ValueOrThrow();
        after.Drifted.Should().BeEmpty();
        after.Pending.Should().BeEmpty();
        (await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None)).ValueOrThrow().Should().BeEmpty();
    }

    [Fact]
    public async Task Repair_ShouldThrow_WhenAllowRollbackDisabled()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");

        await using var migrator = CreateMigrator(); // AllowRollback defaults to false
        (await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None)).ValueOrThrow();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => migrator.Runner.RepairAsync(CancellationToken.None));
    }
}
