using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Ef;

/// <summary>Hosted service that applies pending EF Core migrations on application startup for the configured <typeparamref name="TContext"/>.</summary>
public sealed class EfMigrationsHostedService<TContext> : IHostedService
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
            _logger.LogInformation("EF migrations runner is disabled — skipping.");
            return;
        }

        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        for (var attempt = 1; attempt <= _options.MaxConnectAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation(
                    "Applying EF migrations for {Context} (attempt {Attempt}/{Max})",
                    typeof(TContext).Name, attempt, _options.MaxConnectAttempts);

                await context.Database.MigrateAsync(cancellationToken);

                _logger.LogInformation("EF migrations applied for {Context}.", typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxConnectAttempts)
            {
                _logger.LogWarning(ex,
                    "Migration attempt {Attempt} failed; retrying in {Delay}.",
                    attempt, _options.ConnectRetryDelay);

                await Task.Delay(_options.ConnectRetryDelay, cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
