using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Jobs.Batches.BackgroundServices;

namespace WoW.Two.Sdk.Backend.Beta.Jobs.Batches;

/// <summary>Provides registration for in-memory batch pipelines.</summary>
public static class BatchPipelineServiceCollectionExtensions
{
    /// <summary>Registers a bounded pipeline for <typeparamref name="TItem"/> and the loop that hands its batches to <typeparamref name="THandler"/>.</summary>
    /// <typeparam name="TItem">The work item type; one pipeline per type.</typeparam>
    /// <typeparam name="THandler">The scoped batch handler.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the capacity, batch size and partial-batch delay.</param>
    /// <remarks>Producers inject <see cref="IBatchPipeline{TItem}"/>; diagnostics inject <see cref="BoundedBatchPipeline{TItem}"/> for its counts.</remarks>
    public static IServiceCollection AddBatchPipeline<TItem, THandler>(
        this IServiceCollection services,
        Action<BatchPipelineOptions>? configure = null)
        where THandler : class, IBatchHandler<TItem>
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new BatchPipelineOptions { Name = typeof(TItem).Name };
        configure?.Invoke(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Capacity, 1, nameof(configure));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.BatchSize, 1, nameof(configure));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxBatchDelay, TimeSpan.Zero, nameof(configure));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new BoundedBatchPipeline<TItem>(
            options,
            provider.GetRequiredService<ILogger<BoundedBatchPipeline<TItem>>>()));
        services.TryAddSingleton<IBatchPipeline<TItem>>(provider => provider.GetRequiredService<BoundedBatchPipeline<TItem>>());
        services.TryAddScoped<IBatchHandler<TItem>, THandler>();
        services.AddHostedService(provider => new BatchPipelineBackgroundService<TItem>(
            provider.GetRequiredService<BoundedBatchPipeline<TItem>>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<BatchPipelineBackgroundService<TItem>>>()));
        return services;
    }
}
