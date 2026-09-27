using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Caching.Invalidation;
using WoW.Two.Sdk.Backend.Beta.Caching.Invalidation.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Committed invalidations reach every listening host; rollbacks never do; a lost listener resubscribes.</summary>
[Collection(DataTestCollection.Name)]
public sealed class CacheInvalidationTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);
    private readonly string _channel = "wow2_test_" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task CommittedInvalidations_ReachEveryHost_AndRollbacksDoNot()
    {
        var first = new RecordingHandler();
        var second = new RecordingHandler();
        using IHost firstHost = await StartAsync(first, "first");
        using IHost secondHost = await StartAsync(second, "second");
        await WaitUntilAsync(() => first.AllCount == 1 && second.AllCount == 1);

        await using var context = TestDb.NewContext();
        await using (var rolledBack = await context.Database.BeginTransactionAsync())
        {
            await context.Database.PublishCacheKeyInvalidationAsync("code:discarded", _channel);
            await rolledBack.RollbackAsync();
        }

        await using (var committed = await context.Database.BeginTransactionAsync())
        {
            await context.Database.PublishCacheKeyInvalidationAsync("code:abc", _channel);
            await context.Database.PublishCacheTagInvalidationAsync("codes", _channel);
            await Task.Delay(300);
            Assert.Empty(first.Keys);
            await committed.CommitAsync();
        }

        await WaitUntilAsync(() => first.Keys.Contains("code:abc") && second.Keys.Contains("code:abc")
            && first.Tags.Contains("codes") && second.Tags.Contains("codes"));
        Assert.DoesNotContain("code:discarded", first.Keys);
        await StopAsync(firstHost, secondHost);
    }

    [Fact]
    public async Task LostListener_ReconnectsAndEvictsEverything()
    {
        var handler = new RecordingHandler();
        string application = "wow2-invalidation-" + Guid.NewGuid().ToString("N")[..12];
        using IHost host = await StartAsync(handler, application);
        await WaitUntilAsync(() => handler.AllCount == 1);

        await using var context = TestDb.NewContext();
        await context.Database.ExecuteSqlAsync(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name = {application}");
        await WaitUntilAsync(() => handler.AllCount == 2);

        await context.Database.PublishCacheKeyInvalidationAsync("code:after-reconnect", _channel);
        await WaitUntilAsync(() => handler.Keys.Contains("code:after-reconnect"));
        await StopAsync(host);
    }

    [Fact]
    public async Task MemoryCacheHandler_EvictsOnlyTheCommittedKey()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddPostgresCacheInvalidation(options =>
        {
            options.ConnectionString = TestDb.ConnectionString;
            options.Channel = _channel;
        });
        builder.Services.AddMemoryCacheInvalidationHandler();
        using IHost host = builder.Build();
        var cache = host.Services.GetRequiredService<IMemoryCache>();
        cache.Set("code:sentinel", 1);
        await host.StartAsync();
        await WaitUntilAsync(() => !cache.TryGetValue("code:sentinel", out _));   // subscribing evicts everything

        cache.Set("code:abc", 1);
        cache.Set("code:keep", 1);
        await using var context = TestDb.NewContext();
        await context.Database.PublishCacheKeyInvalidationAsync("code:abc", _channel);

        await WaitUntilAsync(() => !cache.TryGetValue("code:abc", out _));
        Assert.True(cache.TryGetValue("code:keep", out _));
        Assert.IsType<MemoryCacheInvalidationHandler>(host.Services.GetRequiredService<ICacheInvalidationHandler>());
        await StopAsync(host);
    }

    [Fact]
    public void Registration_ListensOnThePersistenceDatabase_WhenNoConnectionIsSet()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DatabaseSettings { ConnectionString = TestDb.ConnectionString });
        services.AddPostgresCacheInvalidation();
        using var provider = services.BuildServiceProvider();

        Assert.Equal(TestDb.ConnectionString, provider.GetRequiredService<PostgresCacheInvalidationOptions>().ConnectionString);
    }

    private async Task<IHost> StartAsync(RecordingHandler handler, string application)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ICacheInvalidationHandler>(handler);
        builder.Services.AddPostgresCacheInvalidation(options =>
        {
            options.ConnectionString = TestDb.ConnectionString + ";Application Name=" + application;
            options.Channel = _channel;
            options.ReconnectDelay = TimeSpan.FromMilliseconds(100);
        });
        IHost host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static async Task StopAsync(params IHost[] hosts)
    {
        foreach (IHost host in hosts)
        {
            await host.StopAsync();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The listener did not reach the expected state in time.");
            await Task.Delay(25);
        }
    }

    private sealed class RecordingHandler : ICacheInvalidationHandler
    {
        private int _allCount;

        public ConcurrentBag<string> Keys { get; } = [];

        public ConcurrentBag<string> Tags { get; } = [];

        public int AllCount => Volatile.Read(ref _allCount);

        public ValueTask InvalidateKeyAsync(string key, CancellationToken cancellationToken)
        {
            Keys.Add(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask InvalidateTagAsync(string tag, CancellationToken cancellationToken)
        {
            Tags.Add(tag);
            return ValueTask.CompletedTask;
        }

        public ValueTask InvalidateAllAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _allCount);
            return ValueTask.CompletedTask;
        }
    }
}
