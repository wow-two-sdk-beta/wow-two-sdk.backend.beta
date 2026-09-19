using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>Verifies that database-provider selection belongs to each fixture instance.</summary>
public sealed class RelationalTestDbProviderIsolationTests
{
    [Fact]
    public void Fixtures_ShouldOwnIndependentProviderSelections()
    {
        var postgres = new ProviderTestDb(DatabaseProvider.Postgres);
        var sqlite = new ProviderTestDb(DatabaseProvider.Sqlite);

        postgres.Provider.Should().Be(DatabaseProvider.Postgres);
        sqlite.Provider.Should().Be(DatabaseProvider.Sqlite);
    }

    [Fact]
    public void PostgresPersistence_ShouldResolveHostConfigurationWhenServicesBuild()
    {
        var configuration = new ConfigurationManager();
        configuration["DatabaseSettings:ConnectionString"] = "Host=before-host-hook";
        var services = new ServiceCollection();
        services.AddPostgresPersistence<DataTestDbContext>(configuration);
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseSettings:ConnectionString"] = "Host=host-local"
        });

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<DatabaseSettings>().ConnectionString.Should().Be("Host=host-local");
    }

    private sealed class ProviderTestDb(DatabaseProvider provider)
        : RelationalTestDb<DataTestDbContext>(provider)
    {
        protected override DataTestDbContext CreateContext(DbContextOptionsBuilder<DataTestDbContext> builder)
            => new(builder.Options);
    }
}
