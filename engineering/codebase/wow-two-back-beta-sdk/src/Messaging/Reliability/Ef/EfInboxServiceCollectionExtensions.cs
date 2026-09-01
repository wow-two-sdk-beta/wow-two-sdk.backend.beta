using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

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
