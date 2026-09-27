using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire;

/// <summary>Registers Hangfire background jobs (fire-and-forget, delayed, recurring) with SDK serializer conventions and a processing server; pass storage directly or use a preset.</summary>
public static class HangfireJobsServiceCollectionExtensions
{
    /// <summary>Registers the Hangfire client and server with the given storage.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureStorage">Storage wiring (e.g. <c>cfg => cfg.UseInMemoryStorage()</c>).</param>
    /// <param name="configure">Optional worker-count / queue / polling tuning.</param>
    public static IServiceCollection AddHangfireJobs(
        this IServiceCollection services,
        Action<IGlobalConfiguration> configureStorage,
        Action<HangfireJobsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configureStorage);
        return services.AddHangfireJobs((_, config) => configureStorage(config), configure);
    }

    /// <summary>
    /// Registers the Hangfire client and server, wiring storage from the service provider when the job storage is
    /// first resolved — so host-local configuration (a test's in-memory store, a lazily resolved connection) reaches it.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureStorage">Storage wiring over the built provider (e.g. read settings, then pick a store).</param>
    /// <param name="configure">Optional worker-count / queue / polling tuning.</param>
    public static IServiceCollection AddHangfireJobs(
        this IServiceCollection services,
        Action<IServiceProvider, IGlobalConfiguration> configureStorage,
        Action<HangfireJobsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStorage);

        var options = new HangfireJobsOptions();
        configure?.Invoke(options);

        services.AddHangfire((provider, config) =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();
            configureStorage(provider, config);
        });

        services.AddHangfireServer(server =>
        {
            if (options.WorkerCount is { } workerCount)
            {
                server.WorkerCount = workerCount;
            }

            if (options.Queues.Count > 0)
            {
                server.Queues = [.. options.Queues];
            }

            if (options.SchedulePollingInterval is { } schedulePollingInterval)
            {
                server.SchedulePollingInterval = schedulePollingInterval;
            }
        });

        return services;
    }

    /// <summary>Registers the in-memory storage preset — dev / test / single instance only; jobs are lost on restart.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional worker-count / queue / polling tuning.</param>
    public static IServiceCollection AddInMemoryHangfireJobs(
        this IServiceCollection services,
        Action<HangfireJobsOptions>? configure = null)
        => services.AddHangfireJobs(config => config.UseInMemoryStorage(), configure);
}
