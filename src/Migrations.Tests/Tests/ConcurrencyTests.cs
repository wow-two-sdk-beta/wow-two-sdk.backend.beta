using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>
/// Concurrency (SQLite): two runners apply the SAME source to the SAME file DB at once. SQLite has no advisory
/// lock — <c>busy_timeout</c> serializes the write transactions and the <c>ordinal</c> PK is the backstop, so the
/// net effect is each migration applied exactly once with NO duplicate history rows. One runner may fault on the
/// race (a table-exists / busy error); that is the documented weaker guarantee versus Postgres's whole-loop
/// advisory lock, and the surviving runner still completes the batch. The DB end-state is what we assert.
/// </summary>
public sealed class ConcurrencyTests : SqliteMigratorTestBase
{
    [Fact]
    public async Task ApplyPending_TwoRunnersConcurrently_AppliesEachMigrationExactlyOnce()
    {
        Workspace.Write("001-baseline",
            applySql: "create table t1(id int primary key);",
            rollbackSql: "drop table t1;");
        Workspace.Write("002-second",
            applySql: "create table t2(id int primary key);",
            rollbackSql: "drop table t2;");

        // Two distinct DI graphs = two distinct connection sources, both pointed at the one DB file.
        await using var a = CreateMigrator();
        await using var b = CreateMigrator();

        // Start both apply loops together; SQLite serializes writers, the ordinal PK rejects any double-insert.
        var ta = Task.Run(() => a.Runner.ApplyPendingAsync("runner-a", CancellationToken.None));
        var tb = Task.Run(() => b.Runner.ApplyPendingAsync("runner-b", CancellationToken.None));

        // One racing runner may fault; observe both without rethrowing — the end-state below is the contract.
        await Task.WhenAll(
            ta.ContinueWith(static _ => { }, TaskScheduler.Default),
            tb.ContinueWith(static _ => { }, TaskScheduler.Default));

        // Each migration applied exactly once: both tables exist, history has no duplicate ordinals.
        (await a.HasTableAsync("t1")).Should().BeTrue();
        (await a.HasTableAsync("t2")).Should().BeTrue();
        var history = await a.ReadHistoryAsync();
        history.Select(h => h.Ordinal).Should().Equal(1, 2);
    }
}
