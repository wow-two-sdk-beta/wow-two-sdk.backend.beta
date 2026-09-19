using WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

/// <summary>Serializes the migration tests sharing one resettable PostgreSQL container.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresMigrationCollection : ICollectionFixture<MigratorPostgresFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string Name = "Postgres migrations";
}
