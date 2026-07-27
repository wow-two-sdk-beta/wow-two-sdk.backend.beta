using Testcontainers.Redis;
using WoW.Two.Sdk.Backend.Beta.Testing.Containers;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Containers.Redis;

/// <summary>Async fixture spinning up a Redis container.</summary>
public sealed class RedisFixture : ContainerFixtureBase<RedisContainer>
{
    /// <summary>Default constructor.</summary>
    public RedisFixture() : this(new RedisBuilder().Build()) { }

    /// <summary>Constructor accepting a pre-configured <see cref="RedisContainer"/>.</summary>
    public RedisFixture(RedisContainer container) : base(container) { }

    /// <inheritdoc />
    public override string Name => "redis";

    /// <summary>StackExchange.Redis-compatible connection string.</summary>
    public string ConnectionString => Container.GetConnectionString();
}
