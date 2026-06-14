using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.jobs.hangfire;

/// <summary>
/// Hangfire background jobs — fire-and-forget (<c>IBackgroundJobClient</c>), delayed, and recurring
/// (<c>IRecurringJobManager</c>) — with SDK serializer conventions and a processing server.
/// Storage is the variable: pass it here, or use a preset
/// (<c>AddInMemoryHangfireJobs</c> for dev, <c>AddPostgresHangfireJobs</c> for production).
/// </summary>
public static class HangfireJobsServiceCollectionExtensions
{
    /// <summary>
    /// Registers Hangfire client + server with the given storage.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureStorage">Storage wiring (e.g. <c>cfg => cfg.UseInMemoryStorage()</c>).</param>
    /// <param name="configure">Optional worker-count / queue tuning.</param>
    public static IServiceCollection AddHangfireJobs(
        this IServiceCollection services,
        Action<IGlobalConfiguration> configureStorage,
        Action<HangfireJobsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStorage);

        var options = new HangfireJobsOptions();
        configure?.Invoke(options);

        services.AddHangfire(config =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();
            configureStorage(config);
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
        });

        return services;
    }

    /// <summary>
    /// In-memory storage preset — dev / test / single instance only; jobs are lost on restart.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional worker-count / queue tuning.</param>
    public static IServiceCollection AddInMemoryHangfireJobs(
        this IServiceCollection services,
        Action<HangfireJobsOptions>? configure = null)
        => services.AddHangfireJobs(config => config.UseInMemoryStorage(), configure);
}
