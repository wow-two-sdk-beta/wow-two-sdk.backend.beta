using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>DI registration for dead-letter administration.</summary>
public static class DeadLetterAdminServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IDeadLetterAdmin"/> over whatever <see cref="IDeadLetterRepository"/> is registered — browse,
    /// peek, redrive, quarantine, release, purge. Purely additive: nothing on the consume path changes, and the admin
    /// does nothing until something calls it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional overrides — chiefly <see cref="DeadLetterAdminOptions.MaxRedrives"/>.</param>
    /// <remarks>
    ///   - call in any order relative to the transport — the store is resolved on first use
    ///   - for cross-source browse, by-id lookup and purge, pair with <see cref="AddInMemoryDeadLetterQueryStore"/> or a broker store implementing <see cref="IDeadLetterQueryRepository"/>
    /// </remarks>
    public static IServiceCollection AddDeadLetterAdmin(this IServiceCollection services, Action<DeadLetterAdminOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DeadLetterAdminOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDeadLetterAdmin, DeadLetterAdmin>();
        return services;
    }

    /// <summary>
    /// Replace the dead-letter store with the in-memory <see cref="IDeadLetterQueryRepository"/> — same behaviour as the
    /// default in-memory store plus cross-source browse, by-id lookup, in-place update and delete.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <remarks>
    ///   - overrides a broker adapter's store too, so call it only where the process is the dead-letter terminus
    ///   - a broker with a native DLQ implements <see cref="IDeadLetterQueryRepository"/> in its own store instead
    ///   - state is process-local and does not survive a restart
    /// </remarks>
    public static IServiceCollection AddInMemoryDeadLetterQueryStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IDeadLetterRepository, InMemoryDeadLetterQueryRepository>());
        return services;
    }
}
