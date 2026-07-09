using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>A processed-message marker in the inbox dedupe table (<c>inbox_messages</c>) — turns at-least-once delivery into exactly-once effect.</summary>
public sealed class InboxMessageEntity : IHasTableName
{
    /// <summary>The inbox table name.</summary>
    public static string TableName => "inbox_messages";

    /// <summary>The processed message id (primary key / dedupe key).</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>When the message was first seen (UTC).</summary>
    public DateTimeOffset SeenAtUtc { get; set; }
}

/// <summary>EF mapping for <see cref="InboxMessageEntity"/> — DDL is owned by the bespoke migrator; this maps the CLR type over it.</summary>
internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<InboxMessageEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(InboxMessageEntity.TableName);
        builder.HasKey(entity => entity.MessageId);
    }
}

/// <summary>Model-builder extension registering the inbox entity mapping.</summary>
public static class InboxModelBuilderExtensions
{
    /// <summary>Map <see cref="InboxMessageEntity"/> into the model. Call from the context's <c>OnModelCreating</c> when using the EF inbox.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    public static ModelBuilder ApplyInboxModel(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        return modelBuilder;
    }
}

/// <summary>
/// EF-backed <see cref="IInboxProcessor"/> — runs the handler in the <b>same transaction</b> as the <c>inbox_messages</c>
/// insert, so the dedupe mark and the handler's effect commit atomically (true exactly-once). A PK conflict on the inbox
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
        context.Set<InboxMessageEntity>().Add(new InboxMessageEntity { MessageId = messageId, SeenAtUtc = timeProvider.GetUtcNow() });
        try
        {
            // Flush the inbox row first so a PK conflict here (a concurrent duplicate) is distinguishable from a handler error.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await handler(cancellationToken);                        // the handler's writes enlist in this transaction
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);        // inbox row + effect commit together; a handler throw rolls both back → retry
        return true;
    }
}

/// <summary>DI registration for the EF-backed exactly-once inbox processor.</summary>
public static class EfInboxServiceCollectionExtensions
{
    /// <summary>
    /// Replace the default <see cref="IInboxProcessor"/> with the EF-backed exactly-once one over <typeparamref name="TContext"/>.
    /// The context must map <see cref="InboxMessageEntity"/> (call <c>modelBuilder.ApplyInboxModel()</c>) and the <c>inbox_messages</c>
    /// table must exist. Scoped so it shares the message's DbContext (and thus transaction) with the handler's repositories.
    /// </summary>
    /// <typeparam name="TContext">The application's DbContext.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEfInbox<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.Replace(ServiceDescriptor.Scoped<IInboxProcessor, EfInboxProcessor<TContext>>());
        return services;
    }
}
