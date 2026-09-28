using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="SqlIdempotencyRepository"/> over a shared SQLite database: two repository instances stand in for two hosts
/// sharing one table — acquisition, replay, release, lease takeover, expiry and contract checks.
/// </summary>
[SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Teardown runs in IAsyncLifetime.DisposeAsync, which xUnit invokes.")]
public sealed class SqlIdempotencyRepositoryTests : IAsyncLifetime
{
    private readonly string _connectionString = $"Data Source=idem-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero));
    private SqliteConnection? _keeper;
    private SqlIdempotencyRepository _hostA = null!;
    private SqlIdempotencyRepository _hostB = null!;

    public async Task InitializeAsync()
    {
        _keeper = new SqliteConnection(_connectionString);
        await _keeper.OpenAsync();
        _hostA = Create();
        _hostB = Create();
        await _hostA.EnsureTableAsync();
    }

    public async Task DisposeAsync()
    {
        if (_keeper is not null)
            await _keeper.DisposeAsync();
    }

    [Fact]
    public async Task SecondHost_ShouldSeeTheKeyInProgressAndThenReplayTheStoredResponse()
    {
        var (acquired, _, ownership) = await _hostA.TryAcquireAsync("order-1", typeof(Receipt), default);
        acquired.Should().BeTrue();

        var inProgress = () => _hostB.TryAcquireAsync("order-1", typeof(Receipt), default);
        (await inProgress.Should().ThrowAsync<AppException>()).WithMessage("*still in progress*");

        await _hostA.StoreAsync("order-1", ownership, new Receipt { Number = 42, Total = 9.5m }, TimeSpan.FromHours(1), default);
        var replay = await _hostB.TryAcquireAsync("order-1", typeof(Receipt), default);

        replay.Acquired.Should().BeFalse();
        replay.CachedResponse.Should().BeEquivalentTo(new Receipt { Number = 42, Total = 9.5m });
    }

    [Fact]
    public async Task Release_ShouldFreeTheKeyAndStoreShouldRejectAForeignOwner()
    {
        var first = await _hostA.TryAcquireAsync("order-2", typeof(Receipt), default);
        await _hostA.ReleaseAsync("order-2", first.Ownership, default);

        var second = await _hostB.TryAcquireAsync("order-2", typeof(Receipt), default);
        second.Acquired.Should().BeTrue();

        var foreign = () => _hostA.StoreAsync("order-2", first.Ownership, new Receipt(), TimeSpan.FromHours(1), default);
        await foreign.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LapsedLease_ShouldPassToAnotherHost()
    {
        var crashed = await _hostA.TryAcquireAsync("order-3", typeof(Receipt), default);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var takeover = await _hostB.TryAcquireAsync("order-3", typeof(Receipt), default);

        takeover.Acquired.Should().BeTrue();
        takeover.Ownership.Should().NotBe(crashed.Ownership);
        var late = () => _hostA.StoreAsync("order-3", crashed.Ownership, new Receipt(), TimeSpan.FromHours(1), default);
        await late.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExpiredResponse_ShouldAllowANewExecutionAndBePurged()
    {
        var first = await _hostA.TryAcquireAsync("order-4", typeof(Receipt), default);
        await _hostA.StoreAsync("order-4", first.Ownership, null, TimeSpan.FromMinutes(10), default);
        (await _hostB.TryAcquireAsync("order-4", typeof(Receipt), default)).Should().Be((false, (object?)null, Guid.Empty));

        _clock.Advance(TimeSpan.FromMinutes(10));
        (await _hostB.PurgeExpiredAsync()).Should().Be(1);
        (await _hostB.TryAcquireAsync("order-4", typeof(Receipt), default)).Acquired.Should().BeTrue();
    }

    [Fact]
    public async Task AnotherResponseContract_ShouldConflict()
    {
        var first = await _hostA.TryAcquireAsync("order-5", typeof(Receipt), default);
        await _hostA.StoreAsync("order-5", first.Ownership, new Receipt(), TimeSpan.FromHours(1), default);

        var other = () => _hostB.TryAcquireAsync("order-5", typeof(string), default);

        (await other.Should().ThrowAsync<AppException>()).WithMessage("*another response contract*");
    }

    private SqlIdempotencyRepository Create() => new(new SqliteConnectionFactory(_connectionString), new SqlIdempotencyOptions(), _clock);

    private sealed record Receipt
    {
        public int Number { get; init; }

        public decimal Total { get; init; }
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
