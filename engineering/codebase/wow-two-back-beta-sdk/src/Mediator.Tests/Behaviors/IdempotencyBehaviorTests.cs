using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="IdempotencyBehavior{TRequest,TResponse}"/> — requests marked <see cref="IIdempotent"/> execute once
/// and replay the cached response on a same-key repeat; unmarked requests pass straight through. The
/// <see cref="InMemoryIdempotencyStore"/> honours the supplied TTL.
/// </summary>
public sealed class IdempotencyBehaviorTests
{
    private sealed record PayRequest(string IdempotencyKey, int Amount) : IRequest<int>, IIdempotent;

    private sealed record PlainRequest(int Amount) : IRequest<int>;

    private static InMemoryIdempotencyStore NewStore()
        => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task HandleAsync_ShouldExecuteOnceAndReplayCachedResponse_WhenSameKey()
    {
        var store = NewStore();
        var behavior = new IdempotencyBehavior<PayRequest, int>(store);
        var calls = 0;
        ValueTask<int> Next() { calls++; return ValueTask.FromResult(42); }

        var req = new PayRequest("key-1", 100);
        var first = await behavior.HandleAsync(req, Next, CancellationToken.None);
        var second = await behavior.HandleAsync(req, Next, CancellationToken.None);

        first.Should().Be(42);
        second.Should().Be(42);   // replayed from cache
        calls.Should().Be(1, "the handler must run once; the second same-key call replays the cached response");
    }

    [Fact]
    public async Task HandleAsync_ShouldExecuteHandler_WhenDifferentKeys()
    {
        var store = NewStore();
        var behavior = new IdempotencyBehavior<PayRequest, int>(store);
        var calls = 0;

        await behavior.HandleAsync(new PayRequest("a", 1), () => { calls++; return ValueTask.FromResult(1); }, CancellationToken.None);
        await behavior.HandleAsync(new PayRequest("b", 2), () => { calls++; return ValueTask.FromResult(2); }, CancellationToken.None);

        calls.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_ShouldPassThroughEveryTime_WhenNonIdempotentRequest()
    {
        var store = NewStore();
        var behavior = new IdempotencyBehavior<PlainRequest, int>(store);
        var calls = 0;
        ValueTask<int> Next() { calls++; return ValueTask.FromResult(7); }

        await behavior.HandleAsync(new PlainRequest(1), Next, CancellationToken.None);
        await behavior.HandleAsync(new PlainRequest(1), Next, CancellationToken.None);

        calls.Should().Be(2, "requests not marked IIdempotent are never deduped");
    }

    [Fact]
    public async Task Store_ShouldReplayWithinTtlAndForgetAfterExpiry()
    {
        var store = NewStore();

        // Acquire a slot, then store with a tiny TTL.
        var (acquired, _) = await store.TryAcquireAsync("k", typeof(int), CancellationToken.None);
        acquired.Should().BeTrue();
        await store.StoreAsync("k", 99, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        // Within TTL → cached, not acquirable.
        var (acquiredAgain, cached) = await store.TryAcquireAsync("k", typeof(int), CancellationToken.None);
        acquiredAgain.Should().BeFalse();
        cached.Should().Be(99);

        // After TTL → entry gone, acquirable again.
        await Task.Delay(120);
        var (acquiredAfter, cachedAfter) = await store.TryAcquireAsync("k", typeof(int), CancellationToken.None);
        acquiredAfter.Should().BeTrue("the TTL elapsed so the cached response should be forgotten");
        cachedAfter.Should().BeNull();
    }
}
