using Testcontainers.Kafka;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Containers;

/// <summary>Async fixture spinning up a Kafka container.</summary>
public sealed class KafkaFixture : ContainerFixtureBase<KafkaContainer>
{
    /// <summary>Default constructor.</summary>
    public KafkaFixture() : this(new KafkaBuilder().Build()) { }

    /// <summary>Constructor accepting a pre-configured <see cref="KafkaContainer"/>.</summary>
    public KafkaFixture(KafkaContainer container) : base(container) { }

    /// <inheritdoc />
    public override string Name => "kafka";

    /// <summary>Bootstrap server connection string for the started broker.</summary>
    public string BootstrapServers => Container.GetBootstrapAddress();
}
