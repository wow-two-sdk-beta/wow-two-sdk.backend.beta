using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Caching.Core;
using WoW.Two.Sdk.Backend.Beta.Caching.Hybrid;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Caching;

/// <summary>Two hosts over one L2: an entry written in an old shape is dropped and recomputed, or surfaced, by configuration.</summary>
public sealed class HybridCacheDeserializationTests
{
    [Fact]
    public async Task SameShape_ShouldReadAnotherHostsEntryWithoutTheFactory()
    {
        var l2 = new DictionaryCache();
        await Host(l2).SetAsync("order:1", new OldShape { Name = "first" });

        var read = await Host(l2).GetOrCreateAsync<OldShape>("order:1", _ => throw new InvalidOperationException("factory must not run"));

        read.Name.Should().Be("first");
    }

    [Fact]
    public async Task ChangedShape_ShouldBeDroppedAndRecomputed_ByDefault()
    {
        var l2 = new DictionaryCache();
        await Host(l2).SetAsync("order:1", new OldShape { Name = "first" });
        var reader = Host(l2);

        var read = await reader.GetOrCreateAsync("order:1", _ => ValueTask.FromResult(new NewShape { Count = 7 }));

        read.Count.Should().Be(7);
        (await Host(l2).GetOrCreateAsync<NewShape>("order:1", _ => throw new InvalidOperationException("the recomputed entry is cached"))).Count.Should().Be(7);
    }

    [Fact]
    public async Task ChangedShape_ShouldThrow_WhenTheHostSaysSo()
    {
        var l2 = new DictionaryCache();
        await Host(l2).SetAsync("order:1", new OldShape { Name = "first" });
        var reader = Host(l2, new Dictionary<string, string?> { ["Caching:Hybrid:DeserializationFailure"] = "Throw" });

        var read = async () => await reader.GetOrCreateAsync("order:1", _ => ValueTask.FromResult(new NewShape { Count = 7 }));

        var thrown = (await read.Should().ThrowAsync<CacheDeserializationException>()).Which;
        thrown.Key.Should().Be("order:1");
        thrown.ValueType.Should().Be<NewShape>();
        thrown.InnerException.Should().BeOfType<System.Text.Json.JsonException>();
    }

    private static ICacheRepository Host(IDistributedCache l2, Dictionary<string, string?>? configuration = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration ?? []).Build());
        services.AddSingleton(l2);
        services.AddHybridCaching();
        return services.BuildServiceProvider().GetRequiredService<ICacheRepository>();
    }

    public sealed class OldShape
    {
        public string? Name { get; set; }
    }

    public sealed class NewShape
    {
        public required int Count { get; set; }
    }

    /// <summary>A plain L2; HybridCache ignores <c>MemoryDistributedCache</c> as redundant with L1.</summary>
    private sealed class DictionaryCache : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, byte[]> _items = new(StringComparer.Ordinal);

        public byte[]? Get(string key) => _items.TryGetValue(key, out var value) ? value : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _items[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => _items.TryRemove(key, out _);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }
}
