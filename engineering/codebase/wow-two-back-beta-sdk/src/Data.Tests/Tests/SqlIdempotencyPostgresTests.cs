using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>The durable idempotency table on PostgreSQL: two hosts share acquisition, replay and lease takeover.</summary>
[Collection(DataTestCollection.Name)]
public sealed class SqlIdempotencyPostgresTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task TwoHostsShareAcquisitionReplayAndTakeover()
    {
        await using var dataSource = NpgsqlDataSource.Create(TestDb.ConnectionString);
        var clock = new SteppedClock();
        var options = new SqlIdempotencyOptions { TableName = $"idempotency_{Guid.NewGuid():N}" };
        var hostA = new SqlIdempotencyRepository(new DataSourceConnectionFactory(dataSource), options, clock);
        var hostB = new SqlIdempotencyRepository(new DataSourceConnectionFactory(dataSource), options, clock);
        await hostA.EnsureTableAsync();

        var owned = await hostA.TryAcquireAsync("charge-1", typeof(string), default);
        Assert.True(owned.Acquired);
        await Assert.ThrowsAsync<AppException>(() => hostB.TryAcquireAsync("charge-1", typeof(string), default));

        await hostA.StoreAsync("charge-1", owned.Ownership, "receipt-7", TimeSpan.FromHours(1), default);
        var replay = await hostB.TryAcquireAsync("charge-1", typeof(string), default);
        Assert.False(replay.Acquired);
        Assert.Equal("receipt-7", replay.CachedResponse);

        var crashed = await hostA.TryAcquireAsync("charge-2", typeof(string), default);
        clock.Advance(TimeSpan.FromMinutes(5));
        var takeover = await hostB.TryAcquireAsync("charge-2", typeof(string), default);
        Assert.True(takeover.Acquired);
        await Assert.ThrowsAsync<InvalidOperationException>(() => hostA.StoreAsync("charge-2", crashed.Ownership, "late", TimeSpan.FromHours(1), default));
    }

    private sealed class SteppedClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
