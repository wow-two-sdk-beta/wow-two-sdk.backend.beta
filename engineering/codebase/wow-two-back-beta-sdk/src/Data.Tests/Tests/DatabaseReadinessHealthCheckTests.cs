using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;
using WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks;
using WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>The readiness check reports a reachable PostgreSQL as healthy and an unreachable one as unhealthy.</summary>
[Collection(DataTestCollection.Name)]
public sealed class DatabaseReadinessHealthCheckTests(DataTestDb testDb)
    : RelationalTestBase<DataTestDb, DataTestDbContext>(testDb)
{
    [Fact]
    public async Task ReachableDatabase_IsHealthy()
    {
        HealthReport report = await CheckAsync(TestDb.ConnectionString);

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Contains("ready", report.Entries["database"].Tags);
    }

    [Fact]
    public async Task UnreachableDatabase_IsUnhealthy()
    {
        HealthReport report = await CheckAsync("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2");

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }

    private static async Task<HealthReport> CheckAsync(string connectionString)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<DataTestDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDatabaseReadinessCheck<DataTestDbContext>();
        await using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
    }
}
