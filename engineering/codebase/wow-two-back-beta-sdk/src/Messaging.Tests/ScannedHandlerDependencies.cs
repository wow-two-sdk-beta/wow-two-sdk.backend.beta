using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Extends a test host with the collaborators every handler scanned from this assembly needs.</summary>
/// <remarks>
/// - handler registration is by assembly scan, so one host gets every handler in the suite
/// - under `Development` the container validates on build, and one unregistered collaborator fails every host
/// - `TryAdd` leaves a test that supplies its own configured instance in charge
/// </remarks>
public static class ScannedHandlerDependenciesExtensions
{
    /// <summary>Registers the collaborators the assembly's scanned handlers resolve.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddScannedHandlerDependencies(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<EventCollector>();
        services.TryAddSingleton<ConcurrencyProbe>();

        return services;
    }
}
