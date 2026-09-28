using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>
/// Accesses idempotency records in Redis: one hash per key digest, written by Lua scripts so acquisition, storage and
/// release are atomic across hosts. An in-progress key expires with its lease, so a crashed host never blocks a retry.
/// </summary>
/// <param name="redis">The shared connection.</param>
/// <param name="options">Prefix, lease and serializer settings.</param>
public sealed class RedisIdempotencyRepository(IConnectionMultiplexer redis, RedisIdempotencyOptions options) : IIdempotencyRepository
{
    private static readonly LuaScript Acquire = LuaScript.Prepare(
        """
        if redis.call('EXISTS', @key) == 0 then
          redis.call('HSET', @key, 'owner', @owner, 'contract', @contract, 'completed', '0')
          redis.call('PEXPIRE', @key, @lease)
          return {1}
        end
        local record = redis.call('HMGET', @key, 'contract', 'completed', 'json')
        return {0, record[1], record[2], record[3]}
        """);

    private static readonly LuaScript Store = LuaScript.Prepare(
        """
        if redis.call('HGET', @key, 'owner') ~= @owner or redis.call('HGET', @key, 'completed') ~= '0' then
          return 0
        end
        if @hasJson == '1' then
          redis.call('HSET', @key, 'json', @json)
        end
        redis.call('HSET', @key, 'completed', '1')
        redis.call('PEXPIRE', @key, @ttl)
        return 1
        """);

    private static readonly LuaScript Release = LuaScript.Prepare(
        """
        if redis.call('HGET', @key, 'owner') == @owner and redis.call('HGET', @key, 'completed') == '0' then
          return redis.call('DEL', @key)
        end
        return 0
        """);

    /// <inheritdoc />
    public async Task<(bool Acquired, object? CachedResponse, Guid Ownership)> TryAcquireAsync(string key, Type responseType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(responseType);
        cancellationToken.ThrowIfCancellationRequested();

        var ownership = Guid.NewGuid();
        var contract = responseType.FullName ?? responseType.Name;
        var reply = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(Acquire, new
        {
            key = Key(key),
            owner = ownership.ToString("N"),
            contract,
            lease = (long)options.PendingLease.TotalMilliseconds,
        }))!;
        if ((long)reply[0] == 1)
            return (true, null, ownership);

        if ((string?)reply[2] != "1")
            throw AppErrorFactory.Conflict("An operation with this idempotency key is still in progress.").ToException();
        if (!string.Equals((string?)reply[1], contract, StringComparison.Ordinal))
            throw AppErrorFactory.Conflict("The idempotency key belongs to another response contract.").ToException();

        var json = (string?)reply[3];
        return (false, json is null ? null : JsonSerializer.Deserialize(json, responseType, options.SerializerOptions), Guid.Empty);
    }

    /// <inheritdoc />
    public async Task StoreAsync(string key, Guid ownership, object? response, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var json = response is null ? string.Empty : JsonSerializer.Serialize(response, response.GetType(), options.SerializerOptions);
        var stored = (long)await redis.GetDatabase().ScriptEvaluateAsync(Store, new
        {
            key = Key(key),
            owner = ownership.ToString("N"),
            hasJson = response is null ? "0" : "1",
            json,
            ttl = (long)ttl.TotalMilliseconds,
        });
        if (stored != 1)
            throw new InvalidOperationException("Acquire the idempotency key before storing its response; the lease may have passed to another host.");
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(string key, Guid ownership, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await redis.GetDatabase().ScriptEvaluateAsync(Release, new { key = Key(key), owner = ownership.ToString("N") });
    }

    private RedisKey Key(string key) => options.KeyPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
