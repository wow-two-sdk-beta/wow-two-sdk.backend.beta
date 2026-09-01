using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>DI registration for the PostgreSQL <c>FOR UPDATE SKIP LOCKED</c> outbox claim strategy.</summary>
public static class PostgresSkipLockedOutboxClaimServiceCollectionExtensions
{
    /// <summary>
    /// Replace the default polling claim strategy with the multi-instance-safe
    /// <see cref="PostgresSkipLockedOutboxClaimRepository"/>. Call after <c>AddEfOutboxDispatcher&lt;TContext&gt;()</c> so the
    /// registered <see cref="IOutboxClaimRepository"/> is swapped for scale-out (many dispatcher instances draining one
    /// PostgreSQL outbox concurrently).
    /// </summary>
    /// <typeparam name="TContext">The application's DbContext — the outbox host, matched to the dispatcher registration.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection ReplaceWithPostgresSkipLockedOutboxClaim<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IOutboxClaimRepository, PostgresSkipLockedOutboxClaimRepository>());
        return services;
    }
}
