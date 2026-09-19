using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>
/// EF-backed <see cref="IInboxProcessor"/> — runs the handler in the same transaction as the <c>inbox_messages</c>
/// insert, so the dedupe mark and the handler's effect commit atomically. A PK conflict on the inbox
/// row means the message was already processed.
/// </summary>
/// <typeparam name="TContext">The application's DbContext (shared with the handler's repositories via the message scope).</typeparam>
internal sealed class EfInboxProcessor<TContext>(TContext context, TimeProvider timeProvider) : IInboxProcessor
    where TContext : DbContext
{
    public async ValueTask<bool> ProcessOnceAsync(string messageId, Func<CancellationToken, ValueTask> handler, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentNullException.ThrowIfNull(handler);

        // Fast path: already committed by a prior delivery.
        if (await context.Set<InboxMessageEntity>().AsNoTracking().AnyAsync(entity => entity.MessageId == messageId, cancellationToken))
            return false;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var inboxRow = new InboxMessageEntity { MessageId = messageId, SeenAtUtc = timeProvider.GetUtcNow() };
        context.Set<InboxMessageEntity>().Add(inboxRow);
        try
        {
            // Flush the inbox row first so a PK conflict here (a concurrent duplicate) is distinguishable from a handler error.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            context.Entry(inboxRow).State = EntityState.Detached;
            if (await context.Set<InboxMessageEntity>().AsNoTracking()
                    .AnyAsync(entity => entity.MessageId == messageId, cancellationToken))
                return false;

            throw;
        }

        await handler(cancellationToken);                        // the handler's writes enlist in this transaction
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);        // inbox row + effect commit together; a handler throw rolls both back → retry
        return true;
    }
}
