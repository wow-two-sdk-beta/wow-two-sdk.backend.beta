using Testcontainers.PostgreSql;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Containers;

/// <summary>
/// Async fixture spinning up a PostgreSQL container.
/// </summary>
public sealed class PostgresFixture : ContainerFixtureBase<PostgreSqlContainer>
{
    /// <summary>Default constructor — uses the default Testcontainers Postgres image.</summary>
    public PostgresFixture() : this(new PostgreSqlBuilder().Build()) { }

    /// <summary>Constructor accepting a pre-configured <see cref="PostgreSqlContainer"/>.</summary>
    public PostgresFixture(PostgreSqlContainer container) : base(container) { }

    /// <inheritdoc />
    public override string Name => "postgres";

    /// <summary>The connection string of the started container.</summary>
    public string ConnectionString => Container.GetConnectionString();
}
