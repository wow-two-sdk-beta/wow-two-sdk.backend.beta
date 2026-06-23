using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Ef;

/// <summary>Hosted service that applies pending EF Core migrations on application startup for the configured <typeparamref name="TContext"/>.</summary>
public sealed partial class EfMigrationsHostedService<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly IServiceProvider _services;
    private readonly EfMigrationsOptions _options;
    private readonly ILogger<EfMigrationsHostedService<TContext>> _logger;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="services">The root provider used to create a scope for the context.</param>
    /// <param name="options">The EF migrations runner options.</param>
    /// <param name="logger">The logger for migration progress and retries.</param>
    public EfMigrationsHostedService(
        IServiceProvider services,
        IOptions<EfMigrationsOptions> options,
        ILogger<EfMigrationsHostedService<TContext>> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            LogDisabled(_logger);
            return;
        }

        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        for (var attempt = 1; attempt <= _options.MaxConnectAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                LogApplying(_logger, typeof(TContext).Name, attempt, _options.MaxConnectAttempts);

                await context.Database.MigrateAsync(cancellationToken);

                LogApplied(_logger, typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxConnectAttempts)
            {
                LogAttemptFailed(_logger, attempt, _options.ConnectRetryDelay, ex);

                await Task.Delay(_options.ConnectRetryDelay, cancellationToken);
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
