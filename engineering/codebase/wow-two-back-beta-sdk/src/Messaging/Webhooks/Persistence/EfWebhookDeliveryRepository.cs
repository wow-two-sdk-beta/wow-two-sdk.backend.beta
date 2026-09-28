using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>Accesses finished deliveries in <c>webhook_deliveries</c> of <typeparamref name="TContext"/>, one scope per call.</summary>
/// <typeparam name="TContext">The context hosting the webhook schema.</typeparam>
/// <param name="scopes">Creates a scope per call for the context.</param>
/// <param name="options">Payload keeping; <c>Webhooks:Persistence</c> reloads live.</param>
public sealed class EfWebhookDeliveryRepository<TContext>(IServiceScopeFactory scopes, IOptionsMonitor<WebhookPersistenceOptions> options) : IWebhookDeliveryRepository
    where TContext : DbContext
{
    /// <inheritdoc />
    public async ValueTask AddAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var current = options.CurrentValue;
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        context.Add(new WebhookDeliveryEntity
        {
            Id = Guid.NewGuid(),
            DeliveryId = record.DeliveryId,
            SubscriptionId = record.SubscriptionId,
            EventType = record.EventType,
            Url = record.Url.ToString(),
            Outcome = record.Outcome,
            Attempts = record.Attempts,
            StatusCode = record.StatusCode,
            OccurredAt = record.OccurredAtUtc,
            Payload = current.StorePayloads && record.Payload.Length <= current.MaxStoredPayloadBytes ? record.Payload.ToArray() : null,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<WebhookDeliveryRecord?> FindAsync(string deliveryId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deliveryId);
        await using var scope = scopes.CreateAsyncScope();
        var entity = await scope.ServiceProvider.GetRequiredService<TContext>().Set<WebhookDeliveryEntity>().AsNoTracking()
            .Where(delivery => delivery.DeliveryId == deliveryId)
            .OrderByDescending(delivery => delivery.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WebhookDeliveryRecord>> ListAsync(string subscriptionId, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscriptionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var scope = scopes.CreateAsyncScope();
        var entities = await scope.ServiceProvider.GetRequiredService<TContext>().Set<WebhookDeliveryEntity>().AsNoTracking()
            .Where(delivery => delivery.SubscriptionId == subscriptionId)
            .OrderByDescending(delivery => delivery.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return [.. entities.Select(ToRecord)];
    }

    /// <inheritdoc />
    public async ValueTask<int> PurgeAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TContext>().Set<WebhookDeliveryEntity>()
            .Where(delivery => delivery.OccurredAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static WebhookDeliveryRecord ToRecord(WebhookDeliveryEntity entity) => new()
    {
        DeliveryId = entity.DeliveryId,
        Payload = entity.Payload ?? ReadOnlyMemory<byte>.Empty,
        SubscriptionId = entity.SubscriptionId,
        EventType = entity.EventType,
        Url = new Uri(entity.Url),
        Outcome = entity.Outcome,
        Attempts = entity.Attempts,
        StatusCode = entity.StatusCode,
        OccurredAtUtc = entity.OccurredAt,
    };
}
