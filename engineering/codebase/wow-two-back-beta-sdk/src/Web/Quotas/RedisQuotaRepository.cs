using StackExchange.Redis;

namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Accesses quota counters in Redis through Lua scripts, so hosts share counts and a check never races its increment.</summary>
/// <param name="redis">The shared connection.</param>
/// <param name="prefix">The key prefix. Default <c>wow2:quota:</c>.</param>
public sealed class RedisQuotaRepository(IConnectionMultiplexer redis, string prefix = "wow2:quota:") : IQuotaRepository
{
    private static readonly LuaScript Increment = LuaScript.Prepare(
        """
        local current = tonumber(redis.call('GET', @key) or '0')
        if tonumber(@limit) >= 0 and current + tonumber(@amount) > tonumber(@limit) then
          return -1
        end
        local value = redis.call('INCRBY', @key, @amount)
        if tonumber(@expiry) > 0 and redis.call('PTTL', @key) < 0 then
          redis.call('PEXPIREAT', @key, @expiry)
        end
        return value
        """);

    private static readonly LuaScript Decrement = LuaScript.Prepare(
        """
        if redis.call('EXISTS', @key) == 0 then
          return 0
        end
        local value = redis.call('DECRBY', @key, @amount)
        if value < 0 then
          redis.call('SET', @key, 0, 'KEEPTTL')
          return 0
        end
        return value
        """);

    /// <inheritdoc />
    public async Task<long?> TryIncrementAsync(string key, long amount, long? limit, DateTimeOffset? expiresAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        var value = (long)await redis.GetDatabase().ScriptEvaluateAsync(Increment, new
        {
            key = (RedisKey)(prefix + key),
            amount,
            limit = limit ?? -1,
            expiry = expiresAt?.ToUnixTimeMilliseconds() ?? 0,
        });
        return value < 0 ? null : value;
    }

    /// <inheritdoc />
    public async Task DecrementAsync(string key, long amount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        await redis.GetDatabase().ScriptEvaluateAsync(Decrement, new { key = (RedisKey)(prefix + key), amount });
    }

    /// <inheritdoc />
    public async Task<long> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        var value = await redis.GetDatabase().StringGetAsync(prefix + key);
        return value.HasValue ? (long)value : 0;
    }
}
