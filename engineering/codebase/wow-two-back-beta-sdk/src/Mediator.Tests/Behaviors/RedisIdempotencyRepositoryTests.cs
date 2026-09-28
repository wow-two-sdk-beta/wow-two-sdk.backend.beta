using AwesomeAssertions;
using StackExchange.Redis;
using Testcontainers.Redis;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="RedisIdempotencyRepository"/> against a real Redis: two repositories stand in for two hosts — acquisition,
/// replay, release, lease expiry and contract checks.
/// </summary>
public sealed class RedisIdempotencyRepositoryTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private ConnectionMultiplexer? _connection;
    private RedisIdempotencyRepository _hostA = null!;
    private RedisIdempotencyRepository _hostB = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var options = new RedisIdempotencyOptions { PendingLease = TimeSpan.FromMilliseconds(400) };
        _hostA = new RedisIdempotencyRepository(_connection, options);
        _hostB = new RedisIdempotencyRepository(_connection, options);
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task SecondHost_ShouldSeeTheKeyInProgress_AndThenReplayTheStoredResponse()
    {
        var (acquired, _, ownership) = await _hostA.TryAcquireAsync("order-1", typeof(Receipt), default);
        acquired.Should().BeTrue();
        (await FluentActions.Awaiting(() => _hostB.TryAcquireAsync("order-1", typeof(Receipt), default)).Should().ThrowAsync<AppException>())
            .WithMessage("*still in progress*");

        await _hostA.StoreAsync("order-1", ownership, new Receipt { Number = "R-7", Total = 12.5m }, TimeSpan.FromMinutes(5), default);

        var (again, cached, _) = await _hostB.TryAcquireAsync("order-1", typeof(Receipt), default);
        again.Should().BeFalse();
        cached.Should().BeEquivalentTo(new Receipt { Number = "R-7", Total = 12.5m });
        (await FluentActions.Awaiting(() => _hostB.TryAcquireAsync("order-1", typeof(string), default)).Should().ThrowAsync<AppException>())
            .WithMessage("*another response contract*");
    }

    [Fact]
    public async Task ReleasedOrLapsedKeys_ShouldBeAcquirableAgain()
    {
        var (_, _, first) = await _hostA.TryAcquireAsync("order-2", typeof(Receipt), default);
        await _hostA.ReleaseAsync("order-2", first, default);
        var (reacquired, _, second) = await _hostB.TryAcquireAsync("order-2", typeof(Receipt), default);
        reacquired.Should().BeTrue();

        await Task.Delay(600);
        var (takenOver, _, third) = await _hostA.TryAcquireAsync("order-2", typeof(Receipt), default);
        takenOver.Should().BeTrue("the crashed host's lease expired");
        await FluentActions.Awaiting(() => _hostB.StoreAsync("order-2", second, new Receipt { Number = "late", Total = 0 }, TimeSpan.FromMinutes(1), default))
            .Should().ThrowAsync<InvalidOperationException>("the lease passed to another host");
        await _hostA.StoreAsync("order-2", third, null, TimeSpan.FromMinutes(1), default);
        (await _hostB.TryAcquireAsync("order-2", typeof(Receipt), default)).CachedResponse.Should().BeNull();
    }

    private sealed record Receipt
    {
        public required string Number { get; init; }

        public required decimal Total { get; init; }
    }
}
