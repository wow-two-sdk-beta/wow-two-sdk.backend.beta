using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>
/// Failure mid-batch: a batch of three where #2's Apply.sql is invalid. Each migration runs in its own
/// transaction (SQLite has transactional DDL), so #1 stays applied + recorded, #2 rolls back wholesale (no table,
/// no history row), and #3 is never reached. Fixing #2 and re-running completes the batch from where it stopped.
/// </summary>
public sealed class FailureMidBatchTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task ApplyPending_WithInvalidMiddleMigration_StopsCleanly_AndFixedReRunCompletes()
    {
        Workspace.Write("001-first",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");
        Workspace.Write("002-broken",
            applySql: "create table t2(this is not valid sql);", // intentional syntax error
            rollbackSql: "drop table t2;");
        Workspace.Write("003-third",
            applySql: "create table t3(id int primary key);",
            rollbackSql: "drop table t3;");

        await using var migrator = CreateMigrator();

        // The batch throws when #2 fails to apply.
        await Assert.ThrowsAnyAsync<Exception>(
            () => migrator.Runner.ApplyPendingAsync("test", CancellationToken.None));

        // #1 committed; #2 rolled back its per-file tx (no table); #3 never ran.
        (await migrator.TableExistsAsync("t1")).Should().BeTrue();
        (await migrator.TableExistsAsync("t2")).Should().BeFalse();
        (await migrator.TableExistsAsync("t3")).Should().BeFalse();

        // History records only #1 — #2's row rolled back with its apply, so table state and history agree.
        var history = await migrator.ReadHistoryAsync();
        history.Select(h => h.Ordinal).Should().Equal(1);

        // Status: #2 and #3 still pending, nothing drifted.
        var status = await migrator.Runner.GetStatusAsync(CancellationToken.None);
        status.Applied.Select(a => a.Ordinal).Should().Equal(1);
        status.Pending.Select(p => p.Ordinal).Should().Equal(2, 3);
        status.Drifted.Should().BeEmpty();

        // Fix #2 and re-run: the batch resumes and finishes #2 + #3 (not re-applying #1).
        Workspace.OverwriteApply("002-broken", "create table t2(id int primary key);");
        var applied = await migrator.Runner.ApplyPendingAsync("test", CancellationToken.None);

        applied.Should().BeEquivalentTo(["002-broken", "003-third"]);
        (await migrator.TableExistsAsync("t2")).Should().BeTrue();
        (await migrator.TableExistsAsync("t3")).Should().BeTrue();
        (await migrator.ReadHistoryAsync()).Select(h => h.Ordinal).Should().Equal(1, 2, 3);
    }
}
