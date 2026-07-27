using Testcontainers.RabbitMq;
using WoW.Two.Sdk.Backend.Beta.Testing.Containers;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Containers.RabbitMq;

/// <summary>Async fixture spinning up a RabbitMQ container.</summary>
public sealed class RabbitMqFixture : ContainerFixtureBase<RabbitMqContainer>
{
    /// <summary>Default constructor.</summary>
    public RabbitMqFixture() : this(new RabbitMqBuilder().Build()) { }

    /// <summary>Constructor accepting a pre-configured <see cref="RabbitMqContainer"/>.</summary>
    public RabbitMqFixture(RabbitMqContainer container) : base(container) { }

    /// <inheritdoc />
    public override string Name => "rabbitmq";

    /// <summary>AMQP connection string.</summary>
    public string ConnectionString => Container.GetConnectionString();
}
