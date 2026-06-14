using Testcontainers.Azurite;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Containers;

/// <summary>Async fixture spinning up an Azurite (Azure Storage emulator) container.</summary>
public sealed class AzuriteFixture : ContainerFixtureBase<AzuriteContainer>
{
    /// <summary>Default constructor.</summary>
    public AzuriteFixture() : this(new AzuriteBuilder().Build()) { }

    /// <summary>Constructor accepting a pre-configured <see cref="AzuriteContainer"/>.</summary>
    public AzuriteFixture(AzuriteContainer container) : base(container) { }

    /// <inheritdoc />
    public override string Name => "azurite";

    /// <summary>Azure-Storage-compatible connection string.</summary>
    public string ConnectionString => Container.GetConnectionString();

    /// <summary>Blob service endpoint.</summary>
    public string BlobEndpoint => Container.GetBlobEndpoint();

    /// <summary>Queue service endpoint.</summary>
    public string QueueEndpoint => Container.GetQueueEndpoint();

    /// <summary>Table service endpoint.</summary>
    public string TableEndpoint => Container.GetTableEndpoint();
}
