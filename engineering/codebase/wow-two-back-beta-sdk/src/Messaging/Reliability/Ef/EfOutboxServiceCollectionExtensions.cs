using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

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
