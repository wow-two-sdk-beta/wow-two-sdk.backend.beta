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

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>Background service that polls the outbox and drains pending rows to the bus.</summary>
/// <typeparam name="TContext">The application's DbContext.</typeparam>
internal sealed partial class OutboxDispatcherBackgroundService<TContext>(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    OutboxDispatcherOptions options,
    ILogger<OutboxDispatcherBackgroundService<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options;
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
