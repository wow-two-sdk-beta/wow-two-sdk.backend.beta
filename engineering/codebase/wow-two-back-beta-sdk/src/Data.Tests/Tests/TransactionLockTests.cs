using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Locks.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Per-key transaction locks serialize competing transactions on PostgreSQL and release at commit.</summary>
[Collection(DataTestCollection.Name)]
public sealed class TransactionLockTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    private static readonly TimeSpan Contended = TimeSpan.FromMilliseconds(400);

    [Fact]
    public async Task SameKeyWaitsForTheHoldingTransaction()
    {
        await using var holder = TestDb.NewContext();
        await using var waiter = TestDb.NewContext();
        await using var held = await holder.Database.BeginTransactionAsync();
        await holder.Database.AcquireTransactionLocksAsync(["owner:shared"]);

        await using var waiting = await waiter.Database.BeginTransactionAsync();
        var acquisition = waiter.Database.AcquireTransactionLocksAsync(["owner:shared"]);
        Assert.NotSame(acquisition, await Task.WhenAny(acquisition, Task.Delay(Contended)));

        await held.CommitAsync();
        await acquisition.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task DistinctKeysDoNotWait()
    {
        await using var first = TestDb.NewContext();
        await using var second = TestDb.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await first.Database.AcquireTransactionLocksAsync("owner", [Guid.NewGuid()]);

        await using var secondTransaction = await second.Database.BeginTransactionAsync();
        await second.Database.AcquireTransactionLocksAsync("owner", [Guid.NewGuid()])
            .WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ReversedKeyOrderCannotDeadlock()
    {
        await using var first = TestDb.NewContext();
        await using var second = TestDb.NewContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await using var secondTransaction = await second.Database.BeginTransactionAsync();

        var forward = first.Database.AcquireTransactionLocksAsync(["a", "b", "c"]);
        var backward = second.Database.AcquireTransactionLocksAsync(["c", "b", "a"]);
        Task winner = await Task.WhenAny(forward, backward).WaitAsync(TimeSpan.FromSeconds(10));
        await winner;

        await (winner == forward ? firstTransaction : secondTransaction).CommitAsync();
        await (winner == forward ? backward : forward).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CancellationStopsWaiting()
    {
        await using var holder = TestDb.NewContext();
        await using var waiter = TestDb.NewContext();
        await using var held = await holder.Database.BeginTransactionAsync();
        await holder.Database.AcquireTransactionLocksAsync(["owner:cancelled"]);

        await using var waiting = await waiter.Database.BeginTransactionAsync();
        using var cancellation = new CancellationTokenSource(Contended);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            waiter.Database.AcquireTransactionLocksAsync(["owner:cancelled"], cancellation.Token));
    }

    [Fact]
    public async Task LocksRequireATransactionAndValidKeys()
    {
        await using var context = TestDb.NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Database.AcquireTransactionLocksAsync(["owner:none"]));
        await using var transaction = await context.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => context.Database.AcquireTransactionLocksAsync([" "]));
    }

    [Fact]
    public async Task SqliteTakesNoLockInsideATransaction()
    {
        var options = new DbContextOptionsBuilder<DataTestDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var context = new DataTestDbContext(options);
        await context.Database.OpenConnectionAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.AcquireTransactionLocksAsync(["owner:sqlite"]);
    }
}
