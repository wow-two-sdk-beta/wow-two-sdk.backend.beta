using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>
/// Accesses pending outbox rows through PostgreSQL's multi-instance claim strategy. Claims the oldest pending rows with
/// <c>SELECT … FOR UPDATE SKIP LOCKED</c> inside a transaction it opens on the context, so concurrent dispatchers
/// skip each other's locked rows — every pending row is claimed by exactly one instance (no double-dispatch, no lost row).
/// </summary>
/// <remarks>
///   - opens a transaction on the context
///   - commits it on the next <see cref="DbContext.SavedChanges"/>, releasing the locks
///   - requires the <c>outbox_messages</c> DDL and its snake_case columns (<c>Ef.md</c>)
/// </remarks>
public sealed class PostgresSkipLockedOutboxClaimRepository : IOutboxClaimRepository
{
    // Claim and lock the oldest pending rows, skipping rows another instance holds; {0} is the batch size.
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

        // Commit on the dispatcher's SaveChanges, holding the row locks until it stamps processed_on_utc.
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
