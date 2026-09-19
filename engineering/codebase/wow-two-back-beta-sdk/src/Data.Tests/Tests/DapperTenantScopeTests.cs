using AwesomeAssertions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Verifies server-owned tenant isolation in generated Dapper CRUD SQL.</summary>
public sealed class DapperTenantScopeTests
{
    [Fact]
    public async Task Repository_ShouldScopeReadsAndWritesToTheAmbientTenant()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"wow-two-dapper-tenant-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

        try
        {
            await using (var setup = new SqliteConnection(connectionString))
            {
                await setup.OpenAsync();
                await setup.ExecuteAsync(
                    "CREATE TABLE tenant_entities (id INTEGER PRIMARY KEY, tenant_id TEXT NOT NULL, value TEXT NOT NULL)");
            }

            var factory = new SqliteConnectionFactory(connectionString);
            var naming = new SqlNamingOptions();
            var tenant = new AmbientTenantContext();
            var unscoped = new DapperRepository<TenantEntity, long>(factory, naming);
            var services = new ServiceCollection();
            services.AddSingleton<IDbConnectionFactory>(factory);
            services.AddSingleton<ITenantContext>(tenant);
            services.AddDapperRepository<TenantEntity, long>();
            await using var provider = services.BuildServiceProvider();
            var scoped = provider.GetRequiredService<IRepository<TenantEntity, long>>();

            await unscoped.CreateAsync(new TenantEntity { Id = 2, TenantId = "tenant-b", Value = "protected" });

            tenant.Set("tenant-a");
            var created = new TenantEntity { Id = 1, TenantId = "tenant-b", Value = "created" };
            await scoped.CreateAsync(created);

            created.TenantId.Should().Be("tenant-a");
            (await scoped.CountAsync()).Should().Be(1);
            (await scoped.GetAllAsync()).Select(row => row.Id).Should().Equal(1);
            (await scoped.GetByIdAsync(2)).Should().BeNull();
            (await scoped.ExistsAsync(2)).Should().BeFalse();

            await scoped.UpdateAsync(new TenantEntity { Id = 2, TenantId = "tenant-b", Value = "stolen" });
            (await scoped.DeleteByIdAsync(2)).Should().BeFalse();

            tenant.Set("tenant-b");
            (await scoped.GetByIdAsync(2)).Should().NotBeNull();
            (await scoped.GetByIdAsync(2))!.Value.Should().Be("protected");
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private sealed record TenantEntity : IKeyedEntity<long>, IHasTableName, IHasTenant<string>
    {
        public static string TableName => "tenant_entities";

        public required long Id { get; init; }

        public required string TenantId { get; set; }

        public required string Value { get; set; }
    }
}
