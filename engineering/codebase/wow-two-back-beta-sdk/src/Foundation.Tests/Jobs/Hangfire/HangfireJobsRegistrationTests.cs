using AwesomeAssertions;
using Hangfire;
using Hangfire.InMemory;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire;
using WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire.Postgres;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Jobs.Hangfire;

/// <summary>
/// Hangfire storage wires from the built provider, so settings and connections registered later reach it. Hangfire keeps
/// its storage in process-wide state, so these run in one non-parallel collection.
/// </summary>
[Collection(HangfireStateCollection.Name)]
public sealed class HangfireJobsRegistrationTests
{
    [Fact]
    public void AddHangfireJobs_ShouldWireStorageFromTheProvider_WhenGivenTheProviderOverload()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new StoreChoice { IsInMemory = true });
        services.AddHangfireJobs((provider, config) =>
        {
            if (provider.GetRequiredService<StoreChoice>().IsInMemory) config.UseInMemoryStorage();
        });

        using var built = services.BuildServiceProvider();

        built.GetRequiredService<JobStorage>().Should().BeOfType<InMemoryStorage>();
    }

    [Fact]
    public void AddPostgresHangfireJobs_ShouldUseThePersistenceConnection_WhenNoConnectionIsGiven()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DatabaseSettings { ConnectionString = "Host=localhost;Database=jobs;Username=u;Password=p" });
        services.AddPostgresHangfireJobs(
            configureStorage: storage =>
            {
                storage.PrepareSchemaIfNecessary = false;
                storage.QueuePollInterval = TimeSpan.FromSeconds(1);
            });

        using var built = services.BuildServiceProvider();

        var storage = built.GetRequiredService<JobStorage>().Should().BeOfType<PostgreSqlStorage>().Subject;
        storage.ToString().Should().Contain("DB: jobs");
    }

    /// <summary>A setting the provider-aware storage wiring reads.</summary>
    private sealed record StoreChoice
    {
        public required bool IsInMemory { get; init; }
    }
}

/// <summary>Serializes the tests that touch Hangfire's process-wide storage.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HangfireStateCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "Hangfire process-wide state";
}
