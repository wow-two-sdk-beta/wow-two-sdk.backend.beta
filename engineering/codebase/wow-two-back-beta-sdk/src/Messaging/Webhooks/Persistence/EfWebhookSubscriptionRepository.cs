using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>
/// Accesses webhook subscriptions in <c>webhook_subscriptions</c> of <typeparamref name="TContext"/>, beside the ones
/// seeded in <see cref="WebhookOptions.Subscriptions"/>; a stored subscription wins over a seeded one with its id.
/// Each call runs in its own scope, so the singleton publisher can use it.
/// </summary>
/// <typeparam name="TContext">The context hosting the webhook schema.</typeparam>
/// <param name="scopes">Creates a scope per call for the context.</param>
/// <param name="webhooks">The seeded subscriptions.</param>
/// <param name="options">Secret protection; <c>Webhooks:Persistence</c> reloads live.</param>
/// <param name="protection">Protects stored secrets.</param>
/// <param name="time">Stamps created and updated times.</param>
/// <param name="logger">Records subscriptions whose secret no longer unprotects.</param>
public sealed partial class EfWebhookSubscriptionRepository<TContext>(
    IServiceScopeFactory scopes,
    WebhookOptions webhooks,
    IOptionsMonitor<WebhookPersistenceOptions> options,
    IDataProtectionProvider protection,
    TimeProvider time,
    ILogger<EfWebhookSubscriptionRepository<TContext>> logger) : IWebhookSubscriptionRepository
    where TContext : DbContext
{
    private const string ProtectedPrefix = "dp1:";
    private readonly IDataProtector _protector = protection.CreateProtector("WoW2.Messaging.Webhooks.Secrets.v1");

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WebhookSubscription>> GetMatchingAsync(string eventType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        return [.. (await ListAsync(cancellationToken)).Where(subscription => subscription.Enabled && subscription.Matches(eventType))];
    }

    /// <inheritdoc />
    public async ValueTask AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = time.GetUtcNow();
        var entity = await context.Set<WebhookSubscriptionEntity>().FindAsync([subscription.Id], cancellationToken);
        if (entity is null)
        {
            entity = new WebhookSubscriptionEntity { Id = subscription.Id, CreatedAt = now };
            context.Add(entity);
        }

        entity.Url = subscription.Url.ToString();
        entity.Secret = options.CurrentValue.ProtectSecrets ? ProtectedPrefix + _protector.Protect(subscription.Secret) : subscription.Secret;
        entity.EventTypeFilter = subscription.EventTypeFilter;
        entity.Enabled = subscription.Enabled;
        entity.Description = subscription.Description;
        entity.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<WebhookSubscription?> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using var scope = scopes.CreateAsyncScope();
        var entity = await scope.ServiceProvider.GetRequiredService<TContext>().Set<WebhookSubscriptionEntity>().AsNoTracking()
            .FirstOrDefaultAsync(subscription => subscription.Id == id, cancellationToken);
        return entity is null ? webhooks.Subscriptions.FirstOrDefault(subscription => subscription.Id == id) : ToModel(entity);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WebhookSubscription>> ListAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var entities = await scope.ServiceProvider.GetRequiredService<TContext>().Set<WebhookSubscriptionEntity>().AsNoTracking()
            .OrderBy(subscription => subscription.Id)
            .ToListAsync(cancellationToken);
        var stored = entities.Select(ToModel).OfType<WebhookSubscription>().ToList();
        var ids = entities.Select(entity => entity.Id).ToHashSet(StringComparer.Ordinal);
        return [.. stored, .. webhooks.Subscriptions.Where(seeded => !ids.Contains(seeded.Id))];
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TContext>().Set<WebhookSubscriptionEntity>()
            .Where(subscription => subscription.Id == id)
            .ExecuteDeleteAsync(cancellationToken) == 1;
    }

    /// <summary>The subscription, or null when its protected secret no longer unprotects (a lost key ring).</summary>
    private WebhookSubscription? ToModel(WebhookSubscriptionEntity entity)
    {
        string secret;
        try
        {
            secret = entity.Secret.StartsWith(ProtectedPrefix, StringComparison.Ordinal) ? _protector.Unprotect(entity.Secret[ProtectedPrefix.Length..]) : entity.Secret;
        }
        catch (CryptographicException)
        {
            LogUnreadableSecret(logger, entity.Id);
            return null;
        }

        return new WebhookSubscription
        {
            Id = entity.Id,
            Url = new Uri(entity.Url),
            Secret = secret,
            EventTypeFilter = entity.EventTypeFilter,
            Enabled = entity.Enabled,
            Description = entity.Description,
        };
    }

    [LoggerMessage(EventId = 6904, Level = LogLevel.Error, Message = "Webhook subscription {SubscriptionId} is skipped: its secret no longer unprotects; restore the Data Protection key ring or store the secret again")]
    private static partial void LogUnreadableSecret(ILogger logger, string subscriptionId);
}
