using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Holds the Redis connection, key prefix, in-progress lease and response serializer for Redis idempotency.</summary>
/// <remarks>Set in code with <c>AddRedisIdempotencyRepository(o => …)</c> or in the host section <c>Mediator:Idempotency:Redis</c>, applied last.</remarks>
public sealed record RedisIdempotencyOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Mediator:Idempotency:Redis";

    /// <summary>Gets or sets the Redis connection string; null uses a registered <c>IConnectionMultiplexer</c>.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Gets or sets the prefix of every key. Default <c>idempotency:</c>.</summary>
    public string KeyPrefix { get; set; } = "idempotency:";

    /// <summary>Gets or sets how long an in-progress key blocks duplicates; Redis expires it so another host can take over. Default 5 minutes.</summary>
    public TimeSpan PendingLease { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the serializer settings for stored responses. Default web settings.</summary>
    public JsonSerializerOptions SerializerOptions { get; set; } = new(JsonSerializerDefaults.Web);
}
