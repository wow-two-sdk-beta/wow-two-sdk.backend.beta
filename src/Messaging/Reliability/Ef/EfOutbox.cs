using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>
/// EF-backed transactional outbox. <see cref="EnqueueAsync"/> adds an <see cref="OutboxMessageEntity"/> row to the
/// tracked <typeparamref name="TContext"/> — it commits atomically with the business write on the app's
/// <c>SaveChanges</c>, solving the dual-write problem with no distributed transaction. A dispatcher drains pending
/// rows to the event bus.
/// </summary>
/// <typeparam name="TContext">The application's DbContext.</typeparam>
internal sealed class EfOutbox<TContext>(TContext context) : IOutbox
    where TContext : DbContext
{
    public ValueTask EnqueueAsync(OutboxRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entity = new OutboxMessageEntity
        {
            Id = Guid.TryParse(record.Id, out var id) ? id : Guid.NewGuid(),
            Type = record.Type,
            Payload = record.Payload.ToArray(),
            OccurredOnUtc = record.OccurredOnUtc,
            HeadersJson = JsonSerializer.Serialize(record.Headers),
        };

        context.Set<OutboxMessageEntity>().Add(entity);
        return ValueTask.CompletedTask;
    }
}

/// <summary>DI registration for the EF-backed transactional outbox.</summary>
public static class EfOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Register the EF-backed <see cref="IOutbox"/> over <typeparamref name="TContext"/>. The context must map
    /// <see cref="OutboxMessageEntity"/> (call <c>modelBuilder.ApplyOutboxModel()</c> in <c>OnModelCreating</c>) and the
    /// <c>outbox_messages</c> table must exist (author a bespoke migration — see <c>Ef.md</c>).
    /// </summary>
    /// <typeparam name="TContext">The application's DbContext.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEfOutbox<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IOutbox, EfOutbox<TContext>>();
        return services;
    }
}
