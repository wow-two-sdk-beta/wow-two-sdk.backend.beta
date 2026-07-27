using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>
/// Multi-instance-safe outbox claim strategy for PostgreSQL. Claims the oldest pending rows with
/// <c>SELECT … FOR UPDATE SKIP LOCKED</c> inside a transaction it opens on the context, so concurrent dispatchers
/// skip each other's locked rows — every pending row is claimed by exactly one instance (no double-dispatch, no lost row).
/// </summary>
/// <remarks>
/// The row locks must be held until the dispatcher has stamped <c>processed_on_utc</c> and its <c>SaveChanges</c>
/// commits — otherwise a competing instance could re-claim a still-pending row between the claim and the stamp. The
/// dispatcher owns no transaction (it relies on the implicit <c>SaveChanges</c> transaction), so this strategy opens the
/// transaction at claim time and bridges its commit onto the context's <see cref="DbContext.SavedChanges"/> event: when
/// the dispatcher's <c>SaveChanges</c> completes, the transaction commits and the locks release together. This keeps the
/// dispatcher unchanged — the polling default and this locking version are drop-in interchangeable via <c>Replace</c>.
/// Targets the <c>outbox_messages</c> table (see <see cref="OutboxMessageEntity"/>); the snake_case column names below
/// match the DDL authored for the bespoke migrator (see <c>Ef.md</c>).
/// </remarks>
public sealed class PostgresSkipLockedOutboxClaimStrategy : IOutboxClaimStrategy
{
    // Compile-time constant (CA2100-safe): claims the oldest pending rows and locks them for this transaction,
    // skipping rows another instance already holds. {0} is the batch-size parameter. Table/columns mirror OutboxMessageEntity.
    private const string ClaimPendingSql =
        "SELECT * FROM outbox_messages " +
        "WHERE processed_on_utc IS NULL " +
        "ORDER BY occurred_on_utc " +
        "FOR UPDATE SKIP LOCKED " +
        "LIMIT {0}";

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessageEntity>> ClaimPendingAsync(DbContext context, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        List<OutboxMessageEntity> pending;
        try
        {
            // Call the relational extension explicitly: the mono-lib also references EF Cosmos, which declares a rival FromSqlRaw.
            pending = await RelationalQueryableExtensions
                .FromSqlRaw(context.Set<OutboxMessageEntity>(), ClaimPendingSql, batchSize)
                .ToListAsync(cancellationToken);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }

        if (pending.Count == 0)
        {
            // Nothing claimed — release the (read-only) transaction now; the dispatcher won't call SaveChanges.
            await transaction.DisposeAsync();
            return pending;
        }

        // Hold the row locks until the dispatcher stamps processed_on_utc and saves, then commit so the claim is durable
        // and the locks release together. The dispatcher owns no transaction, so bridge the commit onto its SaveChanges.
        CommitOnSaveChanges(context, transaction);
        return pending;
    }

    private static void CommitOnSaveChanges(DbContext context, IDbContextTransaction transaction)
    {
        var settled = 0;
        EventHandler<SavedChangesEventArgs>? onSaved = null;
        EventHandler<SaveChangesFailedEventArgs>? onFailed = null;

        void Detach()
        {
            context.SavedChanges -= onSaved;
            context.SaveChangesFailed -= onFailed;
        }

        onSaved = (_, _) =>
        {
            if (Interlocked.Exchange(ref settled, 1) != 0)
                return;
            Detach();
            transaction.Commit();
            transaction.Dispose();
        };

        onFailed = (_, _) =>
        {
            if (Interlocked.Exchange(ref settled, 1) != 0)
                return;
            Detach();
            transaction.Rollback();
            transaction.Dispose();
        };

        context.SavedChanges += onSaved;
        context.SaveChangesFailed += onFailed;
    }
}

/// <summary>DI registration for the PostgreSQL <c>FOR UPDATE SKIP LOCKED</c> outbox claim strategy.</summary>
public static class PostgresSkipLockedOutboxClaimServiceCollectionExtensions
{
    /// <summary>
    /// Replace the default polling claim strategy with the multi-instance-safe
    /// <see cref="PostgresSkipLockedOutboxClaimStrategy"/>. Call after <c>AddEfOutboxDispatcher&lt;TContext&gt;()</c> so the
    /// registered <see cref="IOutboxClaimStrategy"/> is swapped for scale-out (many dispatcher instances draining one
    /// PostgreSQL outbox concurrently).
    /// </summary>
    /// <typeparam name="TContext">The application's DbContext — the outbox host, matched to the dispatcher registration.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection ReplaceWithPostgresSkipLockedOutboxClaim<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IOutboxClaimStrategy, PostgresSkipLockedOutboxClaimStrategy>());
        return services;
    }
}
