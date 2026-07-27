using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

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

        var isGuidId = Guid.TryParse(record.Id, out var id);

        var entity = new OutboxMessageEntity
        {
            Id = isGuidId ? id : Guid.NewGuid(),
            Type = record.Type,
            Payload = record.Payload.ToArray(),
            // The record arrives already serialized, so stamp the format the registered serializer produces — the same
            // content type the transport carries on the wire envelope. Dispatch checks it before deserializing.
            ContentType = serializer.ContentType,
            OccurredOnUtc = record.OccurredOnUtc,
            HeadersJson = SerializeHeaders(record, isGuidId),
        };

        context.Set<OutboxMessageEntity>().Add(entity);
        return ValueTask.CompletedTask;
    }

    // Deliberately System.Text.Json, not IMessageSerializer: headers_json is a text metadata column, not the event body
    // — a binary serializer (MessagePack, Protobuf) has no lossless representation in it.
    private static string SerializeHeaders(OutboxRecord record, bool isGuidId)
    {
        var headers = record.Headers;

        // A caller id that is not a Guid cannot be the row's Guid primary key, and dispatch falls back to the row id as
        // the transport message id — so carry the original through the reserved message-id header instead of losing it
        // to the generated Guid. Dispatch lifts it back onto PublishOptions.MessageId.
        if (!isGuidId && !string.IsNullOrEmpty(record.Id))
        {
            var withMessageId = headers is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(headers, StringComparer.Ordinal);
            withMessageId[MessageHeaders.MessageId] = record.Id;
            headers = withMessageId;
        }

        // Persist the column's own empty-object default rather than a JSON null, so an empty header set reads back the
        // same whether the row was staged with none or with an unset dictionary.
        return headers is { Count: > 0 } ? JsonSerializer.Serialize(headers) : "{}";
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
