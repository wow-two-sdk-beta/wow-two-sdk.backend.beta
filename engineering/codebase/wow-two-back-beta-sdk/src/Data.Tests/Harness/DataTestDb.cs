using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>The suite's shared Postgres test database — one container for the whole collection, truncated per test.</summary>
/// <remarks><see cref="Provider"/> is pinned rather than read from <c>TestSetupOptions</c>: SQLite has no <c>xmin</c>, no <c>FOR UPDATE</c> and different savepoint semantics, and its <c>DataSource=:memory:</c> connection string makes a second connection open a different, empty database — a green SQLite run here would assert on nothing.</remarks>
public sealed class DataTestDb : RelationalTestDb<DataTestDbContext>
{
    /// <inheritdoc />
    public override DatabaseProvider Provider => DatabaseProvider.Postgres;

    /// <inheritdoc />
    /// <remarks>snake_case matches <c>AddPostgresPersistence</c>'s own convention, so the DDL <c>EnsureCreated</c> emits is the DDL the flagship registration maps over — including the outbox columns the claim strategy's raw SQL names. Applied here, not in <see cref="CreateContext"/>, so the DI-built path gets it too.</remarks>
    protected override void ApplyConventions(DbContextOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSnakeCaseNamingConvention();
    }

    /// <inheritdoc />
    protected override DataTestDbContext CreateContext(DbContextOptionsBuilder<DataTestDbContext> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return new DataTestDbContext(builder.Options);
    }
}

/// <summary>The collection sharing one <see cref="DataTestDb"/> container across every test class in this suite.</summary>
[CollectionDefinition(Name)]
public sealed class DataTestCollection : ICollectionFixture<DataTestDb>
{
    /// <summary>The collection name test classes attach to.</summary>
    public const string Name = "data";
}
