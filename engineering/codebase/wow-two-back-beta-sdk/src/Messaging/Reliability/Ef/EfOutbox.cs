using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

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
            // Stamp the serializer's wire content type on the staged payload.
            ContentType = serializer.ContentType,
            OccurredOnUtc = record.OccurredOnUtc,
            HeadersJson = SerializeHeaders(record, isGuidId),
        };

        context.Set<OutboxMessageEntity>().Add(entity);
        return ValueTask.CompletedTask;
    }

    // headers_json is a text column, which a binary message serializer cannot fill losslessly.
    private static string SerializeHeaders(OutboxRecord record, bool isGuidId)
    {
        var headers = record.Headers;

        // Preserve a non-Guid caller message id in the reserved header.
        if (!isGuidId && !string.IsNullOrEmpty(record.Id))
        {
            var withMessageId = headers is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(headers, StringComparer.Ordinal);
            withMessageId[MessageHeaderConstants.MessageId] = record.Id;
            headers = withMessageId;
        }

        // Persist empty headers as the column's empty JSON object.
        return headers is { Count: > 0 } ? JsonSerializer.Serialize(headers) : "{}";
    }
}
