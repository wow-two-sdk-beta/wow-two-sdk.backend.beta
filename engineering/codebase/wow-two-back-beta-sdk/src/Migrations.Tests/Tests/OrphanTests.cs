using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>
/// Orphan: an applied history row whose source folder is gone (binary older than DB, or folder deleted). Apply
/// fails closed (<see cref="MigrationOrphanException"/>) unless <c>AllowOrphanedHistory</c> lets it proceed.
/// </summary>
public sealed class OrphanTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task ApplyPending_ShouldThrowOrphan_WhenAppliedSourceDeleted()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");
        Workspace.Write("002-second",
            applySql: "create table t2(id int primary key);",
            rollbackSql: "drop table t2;");

        await using var migrator = CreateMigrator();
        await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);

        // Delete the source of an applied migration → its history row is now orphaned.
        Workspace.DeleteFolder("002-second");

        // Status reports the orphaned ordinal.
        var status = await migrator.Runner.GetStatusAsync(CancellationToken.None);
        status.Orphaned.Should().Contain(2);

        // Apply fails closed, naming the orphaned ordinal.
        var ex = await Assert.ThrowsAsync<MigrationOrphanException>(
            () => migrator.Runner.ApplyPendingAsync("test", CancellationToken.None));
        ex.OrphanedOrdinals.Should().Contain(2);
    }

    [Fact]
    public async Task ApplyPending_ShouldProceed_WhenOrphanAllowed()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");
        Workspace.Write("002-second",
            applySql: "create table t2(id int primary key);",
            rollbackSql: "drop table t2;");

        // First migrator applies both (orphans not yet relevant).
        await using (var seed = CreateMigrator())
            await seed.Runner.ApplyPendingAsync("test", CancellationToken.None);

        Workspace.DeleteFolder("002-second");

        // A later migration plus a migrator that tolerates orphaned history applies the pending work without throwing.
        Workspace.Write("003-third",
            applySql: "create table t3(id int primary key);",
            rollbackSql: "drop table t3;");

        await using var migrator = CreateMigrator(o => o.AllowOrphanedHistory = true);
        var applied = await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);

        applied.Should().Contain("003-third");
        (await migrator.HasTableAsync("t3")).Should().BeTrue();
        // Orphan still reported in status, but it no longer blocks apply.
        (await migrator.Runner.GetStatusAsync(CancellationToken.None)).Orphaned.Should().Contain(2);
    }
}
