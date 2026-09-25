using Microsoft.Extensions.Caching.Memory;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;
using WoW.Two.Sdk.Backend.Beta.Mediator.Result;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

public sealed class IdempotencyOwnershipTests
{
    [Fact]
    public async Task SimultaneousDuplicatesExecuteOneHandler()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyRepository(cache);
        var behavior = new DeduplicatingInterceptor<Payment, int>(store);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        async ValueTask<int> Handle()
        {
            calls++;
            entered.SetResult();
            await release.Task;
            return 42;
        }
        Task<int> owner = behavior.HandleAsync(new Payment("same"), Handle, CancellationToken.None).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await Assert.ThrowsAsync<AppException>(() =>
                behavior.HandleAsync(new Payment("same"), Handle, CancellationToken.None).AsTask());
        }
        finally
        {
            release.SetResult();
        }
        Assert.Equal(42, await owner);
        Assert.Equal(42, await behavior.HandleAsync(new Payment("same"), Handle, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task StaleOwnerCannotStoreOrReleaseNewReservation()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyRepository(cache);
        var first = await store.TryAcquireAsync("key", typeof(int), CancellationToken.None);
        await store.ReleaseAsync("key", first.Ownership, CancellationToken.None);
        var second = await store.TryAcquireAsync("key", typeof(int), CancellationToken.None);
        await store.ReleaseAsync("key", first.Ownership, CancellationToken.None);
        await Assert.ThrowsAsync<AppException>(() => store.TryAcquireAsync("key", typeof(int), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.StoreAsync("key", first.Ownership, 1, TimeSpan.FromMinutes(1), CancellationToken.None));
        await store.StoreAsync("key", second.Ownership, 2, TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.Equal(2, (await store.TryAcquireAsync("key", typeof(int), CancellationToken.None)).CachedResponse);
    }

    [Fact]
    public async Task ExceptionAndCancellationReleaseOwnership()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var behavior = new DeduplicatingInterceptor<Payment, int>(new InMemoryIdempotencyRepository(cache));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(new Payment("retry"), () => throw new InvalidOperationException(), CancellationToken.None).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            behavior.HandleAsync(new Payment("retry"), () => throw new OperationCanceledException(), CancellationToken.None).AsTask());
        Assert.Equal(9, await behavior.HandleAsync(new Payment("retry"), () => ValueTask.FromResult(9), CancellationToken.None));
    }

    [Fact]
    public async Task CallerCancellationAfterSuccessStillPublishesReplay()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var behavior = new DeduplicatingInterceptor<Payment, int>(new InMemoryIdempotencyRepository(cache));
        using var cancellation = new CancellationTokenSource();
        int calls = 0;
        ValueTask<int> Handle()
        {
            calls++;
            cancellation.Cancel();
            return ValueTask.FromResult(17);
        }
        Assert.Equal(17, await behavior.HandleAsync(new Payment("completed"), Handle, cancellation.Token));
        Assert.Equal(17, await behavior.HandleAsync(new Payment("completed"), Handle, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ReplayStoreFailureRetainsOwnershipAfterSuccessfulEffect()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new FailingStore(new InMemoryIdempotencyRepository(cache));
        var behavior = new DeduplicatingInterceptor<Payment, int>(store);
        int calls = 0;
        ValueTask<int> Handle() => ValueTask.FromResult(++calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(new Payment("store-failure"), Handle, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<AppException>(() =>
            behavior.HandleAsync(new Payment("store-failure"), Handle, CancellationToken.None).AsTask());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AllSdkFailureCarriersRemainRetryable()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyRepository(cache);
        await AssertRetryable(store, Foundation.Results.Result.Fail(AppErrorFactory.Conflict("failure")));
        await AssertRetryable(store, Result<int>.Fail(AppErrorFactory.Conflict("failure")));
        await AssertRetryable(store, Result<int, string>.Fail("failure"));
        await AssertRetryable(store, AppResult<int>.Fail(AppErrorFactory.Conflict("failure")));
    }

    [Fact]
    public async Task NullSuccessIsReplayedAndDifferentResponseTypeIsRejected()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyRepository(cache);
        var behavior = new DeduplicatingInterceptor<Payment, string?>(store);
        int calls = 0;
        ValueTask<string?> Handle() { calls++; return ValueTask.FromResult<string?>(null); }
        Assert.Null(await behavior.HandleAsync(new Payment("null"), Handle, CancellationToken.None));
        Assert.Null(await behavior.HandleAsync(new Payment("null"), Handle, CancellationToken.None));
        Assert.Equal(1, calls);
        var different = new DeduplicatingInterceptor<Payment, int>(store);
        await Assert.ThrowsAsync<AppException>(() =>
            different.HandleAsync(new Payment("null"), () => ValueTask.FromResult(1), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task RequestTenantAndPrincipalPartitionTheKey()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new InMemoryIdempotencyRepository(cache);
        var tenant = new AmbientTenantContext();
        var principal = new Principal { Id = Guid.NewGuid() };
        var behavior = new DeduplicatingInterceptor<Payment, int>(store, tenant: tenant, currentUser: principal);
        int calls = 0;
        ValueTask<int> Handle() => ValueTask.FromResult(++calls);
        tenant.Set("one");
        Assert.Equal(1, await behavior.HandleAsync(new Payment("key"), Handle, CancellationToken.None));
        tenant.Set("two");
        Assert.Equal(2, await behavior.HandleAsync(new Payment("key"), Handle, CancellationToken.None));
        principal.Id = Guid.NewGuid();
        Assert.Equal(3, await behavior.HandleAsync(new Payment("key"), Handle, CancellationToken.None));
        var other = new DeduplicatingInterceptor<OtherPayment, int>(store, tenant: tenant, currentUser: principal);
        Assert.Equal(4, await other.HandleAsync(new OtherPayment("key"), Handle, CancellationToken.None));
        Assert.Equal(3, await behavior.HandleAsync(new Payment("key"), Handle, CancellationToken.None));
    }

    private static async Task AssertRetryable<T>(InMemoryIdempotencyRepository store, T failure)
    {
        var behavior = new DeduplicatingInterceptor<Payment, T>(store);
        int calls = 0;
        ValueTask<T> Handle() { calls++; return ValueTask.FromResult(failure); }
        var request = new Payment(typeof(T).FullName!);
        await behavior.HandleAsync(request, Handle, CancellationToken.None);
        await behavior.HandleAsync(request, Handle, CancellationToken.None);
        Assert.Equal(2, calls);
    }

    private sealed record Payment(string IdempotencyKey) : IIdempotent;
    private sealed record OtherPayment(string IdempotencyKey) : IIdempotent;
    private sealed class Principal : ICurrentUserService
    {
        public Guid? Id { get; set; }
        public UserKind Kind => UserKind.User;
    }

    private sealed class FailingStore(IIdempotencyRepository inner) : IIdempotencyRepository
    {
        public Task<(bool Acquired, object? CachedResponse, Guid Ownership)> TryAcquireAsync(
            string key, Type responseType, CancellationToken cancellationToken)
            => inner.TryAcquireAsync(key, responseType, cancellationToken);

        public Task StoreAsync(string key, Guid ownership, object? response, TimeSpan ttl, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Replay publication unavailable");

        public Task ReleaseAsync(string key, Guid ownership, CancellationToken cancellationToken)
            => inner.ReleaseAsync(key, ownership, cancellationToken);
    }
}
