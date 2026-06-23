using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Data;

/// <summary>Startup helper that creates the database if missing then applies pending bespoke migrations — the run-once boot step for hosts registered via <see cref="PostgresPersistenceServiceCollectionExtensions.AddPostgresPersistence{TContext}"/>.</summary>
public static class PostgresPersistenceStartupExtensions
{
    /// <summary>Creates the target database if it does not exist, then applies all pending bespoke migrations stamped <c>startup</c> (idempotent — safe to run on every host boot).</summary>
    /// <param name="services">The root service provider to open a scope from.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public static async ValueTask MigrateBespokeOnStartupAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var connectionString = serviceProvider.GetRequiredService<DatabaseOptions>().ConnectionString;

        // Create the target database via the maintenance DB before any migration runs.
        var dialect = serviceProvider.GetRequiredService<IMigrationDialect>();
        await dialect.EnsureDatabaseExistsAsync(connectionString, cancellationToken).ConfigureAwait(false);

        var runner = serviceProvider.GetRequiredService<IMigrationRunnerService>();
        await runner.ApplyPendingAsync("startup", cancellationToken).ConfigureAwait(false);
    }
}
