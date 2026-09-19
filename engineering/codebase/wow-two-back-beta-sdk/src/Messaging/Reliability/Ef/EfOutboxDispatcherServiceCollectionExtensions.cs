using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>DI registration for the EF-backed outbox dispatcher.</summary>
public static class EfOutboxDispatcherServiceCollectionExtensions
{
    /// <summary>
    /// Register the outbox dispatcher + its polling hosted service over <typeparamref name="TContext"/>. Requires
    /// <c>AddEfOutbox&lt;TContext&gt;()</c> and a registered <see cref="IEventBus"/>.
    /// </summary>
    /// <typeparam name="TContext">The application's DbContext.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional dispatcher options (poll interval, batch size).</param>
    public static IServiceCollection AddEfOutboxDispatcher<TContext>(
        this IServiceCollection services,
        Action<OutboxDispatcherOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatedOptions<OutboxDispatcherOptions>(
            configure,
            builder => builder
                .Validate(options => options.PollInterval > TimeSpan.Zero, "OutboxDispatcherOptions.PollInterval must be positive.")
                .Validate(options => options.BatchSize > 0, "OutboxDispatcherOptions.BatchSize must be positive.")
                .Validate(options => options.MaxDispatchAttempts > 0, "OutboxDispatcherOptions.MaxDispatchAttempts must be positive.")
                .Validate(options => options.RetentionPeriod > TimeSpan.Zero, "OutboxDispatcherOptions.RetentionPeriod must be positive.")
                .Validate(options => options.PruneInterval > TimeSpan.Zero, "OutboxDispatcherOptions.PruneInterval must be positive."));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<OutboxEventPublisher>();
        services.TryAddSingleton<IOutboxClaimRepository, UnlockedOutboxClaimRepository>();
        services.TryAddScoped<IOutboxDispatcher, OutboxDispatcher<TContext>>();
        services.AddHostedService<OutboxDispatcherBackgroundService<TContext>>();
        return services;
    }
}
