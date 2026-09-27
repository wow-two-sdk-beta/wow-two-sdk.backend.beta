using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks;

/// <summary>Reports whether the database behind <typeparamref name="TContext"/> accepts connections.</summary>
/// <typeparam name="TContext">The relational context the host serves requests with.</typeparam>
/// <param name="scopes">The factory that resolves a context per probe.</param>
public sealed class DatabaseReadinessHealthCheck<TContext>(IServiceScopeFactory scopes) : IHealthCheck
    where TContext : DbContext
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TContext database = scope.ServiceProvider.GetRequiredService<TContext>();
            return await database.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "The database does not accept connections.");
        }
    }
}
