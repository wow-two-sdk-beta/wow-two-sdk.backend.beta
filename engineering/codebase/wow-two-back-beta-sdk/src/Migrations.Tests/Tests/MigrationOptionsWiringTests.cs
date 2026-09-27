using System.Reflection;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Tests;

/// <summary>The Postgres floor forwards migrator tuning, and a product version maps to its <c>vX.Y</c> stamp.</summary>
public sealed class MigrationOptionsWiringTests
{
    [Fact]
    public void AddPostgresPersistence_ShouldApplyTheMigrationHook_WhenOneIsConfigured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionString"] = "Host=localhost;Database=unused;Username=u;Password=p",
            })
            .Build();

        services.AddPostgresPersistence<WiringContext>(configuration, options =>
        {
            options.ConnectionStringEnvironmentVariable = "WOW_TWO_MIGRATIONS_TESTS_NO_SUCH_ENV_VAR";
            options.ConfigureMigrations = migrations => migrations.Version = "v0.5";
        });
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<MigrationOptions>().Version.Should().Be("v0.5");
    }

    [Fact]
    public void ToVersion_ShouldStampMajorAndMinor_WhenTheAssemblyCarriesAVersion()
    {
        var assembly = new VersionedAssembly(new Version(0, 5, 3, 0));

        MigrationVersionMapper.ToVersion(assembly).Should().Be("v0.5");
    }

    [Fact]
    public void ToVersion_ShouldStampZero_WhenTheAssemblyCarriesNoVersion()
    {
        MigrationVersionMapper.ToVersion(new VersionedAssembly(null)).Should().Be("v0.0");
    }

    /// <summary>A context the wiring test registers; it never opens a connection.</summary>
    private sealed class WiringContext(DbContextOptions<WiringContext> options) : DbContext(options);

    /// <summary>An assembly whose name reports a chosen version.</summary>
    private sealed class VersionedAssembly(Version? version) : Assembly
    {
        public override AssemblyName GetName() => new("Product") { Version = version };
    }
}
