using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Observability.HealthChecks;

/// <summary>Extends health-check registration with a database readiness probe.</summary>
public static class DatabaseReadinessHealthChecksBuilderExtensions
{
    /// <summary>Adds a check that fails while the database behind <typeparamref name="TContext"/> refuses connections.</summary>
    /// <typeparam name="TContext">The relational context the host serves requests with.</typeparam>
    /// <param name="builder">The health-check builder.</param>
    /// <param name="name">The check name. Defaults to <c>database</c>.</param>
    /// <param name="tags">The check tags. Defaults to <c>ready</c>.</param>
    public static IHealthChecksBuilder AddDatabaseReadinessCheck<TContext>(
        this IHealthChecksBuilder builder,
        string name = "database",
        IEnumerable<string>? tags = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return builder.AddCheck<DatabaseReadinessHealthCheck<TContext>>(name, failureStatus: null, tags ?? ["ready"]);
    }
}
