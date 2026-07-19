using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>
/// EF-backed transactional outbox. <see cref="EnqueueAsync"/> adds an <see cref="OutboxMessageEntity"/> row to the
/// tracked <typeparamref name="TContext"/> — it commits atomically with the business write on the app's
/// <c>SaveChanges</c>, solving the dual-write problem with no distributed transaction. A dispatcher drains pending
/// rows to the event bus.
/// </summary>
/// <typeparam name="TContext">The application's DbContext.</typeparam>
internal sealed class EfOutbox<TContext>(TContext context, IMessageSerializer serializer) : IOutbox
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
            // The record arrives already serialized, so stamp the format the registered serializer produces — the same
            // content type the transport carries on the wire envelope. Dispatch checks it before deserializing.
            ContentType = serializer.ContentType,
            OccurredOnUtc = record.OccurredOnUtc,
            // Deliberately System.Text.Json, not IMessageSerializer: headers_json is a text metadata column, not the
            // event body — a binary serializer (MessagePack, Protobuf) has no lossless representation in it.
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

        // Default serializer, in case the outbox is registered for staging only (no transport/bus registration, which
        // is what normally supplies it via AddEventResilienceDefaults). TryAdd → a registered serializer still wins.
        services.TryAddSingleton<IMessageSerializer, SystemTextJsonMessageSerializer>();
        services.TryAddScoped<IOutbox, EfOutbox<TContext>>();
        return services;
    }
}
