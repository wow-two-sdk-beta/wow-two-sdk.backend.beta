using Testcontainers.MongoDb;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Containers;

/// <summary>Async fixture spinning up a MongoDB container.</summary>
public sealed class MongoDbFixture : ContainerFixtureBase<MongoDbContainer>
{
    /// <summary>Default constructor.</summary>
    public MongoDbFixture() : this(new MongoDbBuilder().Build()) { }

    /// <summary>Constructor accepting a pre-configured <see cref="MongoDbContainer"/>.</summary>
    public MongoDbFixture(MongoDbContainer container) : base(container) { }

    /// <inheritdoc />
    public override string Name => "mongodb";

    /// <summary>MongoDB connection string.</summary>
    public string ConnectionString => Container.GetConnectionString();
}
