using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>Publishes a runtime-typed event to the <see cref="IEventBus"/> via a cached compiled delegate (bridges the generic <c>PublishAsync&lt;TEvent&gt;</c>).</summary>
internal sealed class OutboxEventPublisher(IEventBus bus)
{
    private static readonly ConcurrentDictionary<Type, Func<IEventBus, object, CancellationToken, ValueTask>> Invokers = new();

    public ValueTask PublishAsync(Type eventType, object @event, CancellationToken cancellationToken)
        => Invokers.GetOrAdd(eventType, BuildInvoker)(bus, @event, cancellationToken);

    private static Func<IEventBus, object, CancellationToken, ValueTask> BuildInvoker(Type eventType)
    {
        var method = typeof(IEventBus).GetMethod(nameof(IEventBus.PublishAsync))!.MakeGenericMethod(eventType);
        var busParam = Expression.Parameter(typeof(IEventBus), "bus");
        var eventParam = Expression.Parameter(typeof(object), "event");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var call = Expression.Call(
            busParam,
            method,
            Expression.Convert(eventParam, eventType),
            Expression.Constant(null, typeof(PublishOptions)),
            ctParam);
        return Expression.Lambda<Func<IEventBus, object, CancellationToken, ValueTask>>(call, busParam, eventParam, ctParam).Compile();
    }
}

/// <summary>
/// Strategy for claiming the next batch of pending outbox rows — the seam that lets multi-instance dispatch swap in
/// a locking claim (Postgres <c>FOR UPDATE SKIP LOCKED</c>) without changing the dispatcher. The default polls without
/// locking (single-instance safe).
/// </summary>
public interface IOutboxClaimStrategy
{
    /// <summary>Claim up to <paramref name="batchSize"/> pending rows for this dispatcher to process.</summary>
    /// <param name="context">The outbox DbContext.</param>
    /// <param name="batchSize">Maximum rows to claim.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<OutboxMessageEntity>> ClaimPendingAsync(DbContext context, int batchSize, CancellationToken cancellationToken);
}

/// <summary>Default claim strategy — polls oldest-first without locking. Single-instance safe; for scale-out, register a <c>FOR UPDATE SKIP LOCKED</c> strategy (Postgres, raw SQL) via <c>Replace</c>.</summary>
internal sealed class PollingOutboxClaimStrategy : IOutboxClaimStrategy
{
    public async Task<IReadOnlyList<OutboxMessageEntity>> ClaimPendingAsync(DbContext context, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await context.Set<OutboxMessageEntity>()
            .Where(message => message.ProcessedOnUtc == null)
            .OrderBy(message => message.OccurredOnUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Drains pending <see cref="OutboxMessageEntity"/> rows to the event bus: claim a batch (via <see cref="IOutboxClaimStrategy"/>),
/// resolve the CLR type (via <see cref="IMessageTypeResolver"/>), deserialize the payload (via <see cref="IMessageSerializer"/>),
/// publish, then stamp <c>ProcessedOnUtc</c> (or bump <c>Attempts</c> + record the error).
/// </summary>
/// <typeparam name="TContext">The application's DbContext.</typeparam>
internal sealed partial class OutboxDispatcher<TContext>(
    TContext context,
    OutboxEventPublisher publisher,
    IOutboxClaimStrategy claimStrategy,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    TimeProvider timeProvider,
    IOptions<OutboxDispatcherOptions> options,
    ILogger<OutboxDispatcher<TContext>> logger) : IOutboxDispatcher
    where TContext : DbContext
{
    public async ValueTask<int> DispatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        var opt = options.Value;
        var pending = await claimStrategy.ClaimPendingAsync(context, batchSize, cancellationToken);

        var dispatched = 0;
        foreach (var row in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Stable token first, assembly-qualified-name fallback — the resolver owns both (and caches the fallback).
            var eventType = string.IsNullOrEmpty(row.Type) ? null : typeResolver.ResolveType(row.Type);
            if (eventType is null)
            {
                MarkFailed(row, $"Unresolved event type '{row.Type}'.", opt);
                LogTypeUnresolved(row.Id, row.Type);
                continue;
            }

            // A row staged by a different serializer than the one now registered would deserialize to garbage (or throw
            // opaquely) — fail it explicitly. Rows written before content_type existed are empty, so the check is skipped.
            if (!string.IsNullOrEmpty(row.ContentType) && !string.Equals(row.ContentType, serializer.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                MarkFailed(row, $"Payload content type '{row.ContentType}' does not match the registered serializer '{serializer.ContentType}'.", opt);
                LogContentTypeMismatch(row.Id, row.ContentType, serializer.ContentType);
                continue;
            }

            try
            {
                var @event = serializer.Deserialize(row.Payload, eventType);
                if (@event is null)
                {
                    MarkFailed(row, "Payload deserialized to null.", opt);
                    continue;
                }

                await publisher.PublishAsync(eventType, @event, cancellationToken);
                row.ProcessedOnUtc = timeProvider.GetUtcNow();
                row.Error = null;
                dispatched++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                MarkFailed(row, ex.Message, opt);
                LogDispatchFailed(ex, row.Id);
            }
        }

        if (pending.Count > 0)
            await context.SaveChangesAsync(cancellationToken);

        return dispatched;
    }

    /// <inheritdoc />
    public async ValueTask<int> PruneProcessedAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - retention;
        return await context.Set<OutboxMessageEntity>()
            .Where(row => row.ProcessedOnUtc != null && row.ProcessedOnUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // Bump the attempt count; once it reaches the cap, give up — stamp ProcessedOnUtc so the poison row stops re-selecting
    // forever at the head of every batch (Error is retained, marking the row dead) rather than retrying indefinitely.
    private void MarkFailed(OutboxMessageEntity row, string error, OutboxDispatcherOptions opt)
    {
        row.Attempts++;
        row.Error = error;
        if (row.Attempts >= opt.MaxDispatchAttempts)
            row.ProcessedOnUtc = timeProvider.GetUtcNow();
    }

    [LoggerMessage(EventId = 6201, Level = LogLevel.Error, Message = "Outbox dispatch failed for message {Id}")]
    private partial void LogDispatchFailed(Exception exception, Guid id);

    [LoggerMessage(EventId = 6202, Level = LogLevel.Warning, Message = "Outbox message {Id} has unresolved event type {Type}")]
    private partial void LogTypeUnresolved(Guid id, string type);

    [LoggerMessage(EventId = 6204, Level = LogLevel.Error, Message = "Outbox message {Id} was staged as {RowContentType} but the registered serializer produces {SerializerContentType}")]
    private partial void LogContentTypeMismatch(Guid id, string rowContentType, string serializerContentType);
}

/// <summary>Options for the outbox dispatcher hosted service.</summary>
public sealed class OutboxDispatcherOptions
{
    /// <summary>How often the dispatcher polls for pending rows. Default 5s.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum rows dispatched per pass. Default 100.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Attempts before a poison row is given up on (stamped processed, error retained) so it stops re-selecting forever. Default 10.</summary>
    public int MaxDispatchAttempts { get; set; } = 10;

    /// <summary>How long processed rows are retained before pruning. Default 7 days.</summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How often the dispatcher prunes old processed rows. Default 15 minutes.</summary>
    public TimeSpan PruneInterval { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>Background service that polls the outbox and drains pending rows to the bus.</summary>
/// <typeparam name="TContext">The application's DbContext.</typeparam>
internal sealed partial class OutboxDispatcherHostedService<TContext>(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<OutboxDispatcherOptions> options,
    ILogger<OutboxDispatcherHostedService<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        var lastPrune = timeProvider.GetUtcNow();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                await dispatcher.DispatchAsync(config.BatchSize, stoppingToken);

                if (timeProvider.GetUtcNow() - lastPrune >= config.PruneInterval)
                {
                    await dispatcher.PruneProcessedAsync(config.RetentionPeriod, stoppingToken);
                    lastPrune = timeProvider.GetUtcNow();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogPollFailed(ex);
            }

            try
            {
                await Task.Delay(config.PollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    [LoggerMessage(EventId = 6203, Level = LogLevel.Error, Message = "Outbox dispatcher poll failed")]
    private partial void LogPollFailed(Exception exception);
}

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

        var optionsBuilder = services.AddOptions<OutboxDispatcherOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<OutboxEventPublisher>();
        services.TryAddSingleton<IOutboxClaimStrategy, PollingOutboxClaimStrategy>();
        services.TryAddScoped<IOutboxDispatcher, OutboxDispatcher<TContext>>();
        services.AddHostedService<OutboxDispatcherHostedService<TContext>>();
        return services;
    }
}
