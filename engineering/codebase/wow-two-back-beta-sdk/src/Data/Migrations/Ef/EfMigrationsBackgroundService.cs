using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Ef;

/// <summary>Runs pending EF Core migrations during host startup.</summary>
/// <param name="services">The root provider used to create a scope for the context.</param>
/// <param name="options">The EF migrations runner options.</param>
/// <param name="logger">The logger for migration progress and retries.</param>
public sealed partial class EfMigrationsBackgroundService<TContext>(
    IServiceProvider services,
    EfMigrationsOptions options,
    ILogger<EfMigrationsBackgroundService<TContext>> logger) : IHostedService
    where TContext : DbContext
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        for (var attempt = 1; attempt <= options.MaxConnectAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                LogApplying(logger, typeof(TContext).Name, attempt, options.MaxConnectAttempts);

                await context.Database.MigrateAsync(cancellationToken);

                LogApplied(logger, typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (attempt < options.MaxConnectAttempts)
            {
                LogAttemptFailed(logger, attempt, options.ConnectRetryDelay, ex);

                await Task.Delay(options.ConnectRetryDelay, cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 3201, Level = LogLevel.Information, Message = "EF migrations runner is disabled — skipping.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 3202, Level = LogLevel.Information, Message = "Applying EF migrations for {Context} (attempt {Attempt}/{Max})")]
    private static partial void LogApplying(ILogger logger, string context, int attempt, int max);

    [LoggerMessage(EventId = 3203, Level = LogLevel.Information, Message = "EF migrations applied for {Context}.")]
    private static partial void LogApplied(ILogger logger, string context);

    [LoggerMessage(EventId = 3204, Level = LogLevel.Warning, Message = "Migration attempt {Attempt} failed; retrying in {Delay}.")]
    private static partial void LogAttemptFailed(ILogger logger, int attempt, TimeSpan delay, Exception exception);
}
