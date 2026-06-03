using System.Reflection;
using EntityFrameworkCore.Triggered;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Triggered;

/// <summary>
/// Convenience extensions over <c>EntityFrameworkCore.Triggered</c> — assembly scan registration
/// + DbContextOptionsBuilder pass-through.
/// </summary>
public static class TriggeredExtensions
{
    /// <summary>
    /// Wires triggers into a <see cref="DbContextOptionsBuilder"/>. Triggers themselves are registered
    /// via DI (use <see cref="AddTriggersFromAssemblies"/> or the underlying lib's APIs).
    /// </summary>
    public static DbContextOptionsBuilder UseTriggersConventional(this DbContextOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseTriggers();
    }

    /// <summary>
    /// Scans the given assemblies for trigger implementations and registers them.
    /// Convention method names: <c>IBeforeSaveTrigger&lt;T&gt;</c>, <c>IAfterSaveTrigger&lt;T&gt;</c>, etc.
    /// </summary>
    public static IServiceCollection AddTriggersFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            foreach (var implType in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            {
                foreach (var iface in implType.GetInterfaces())
                {
                    if (!iface.IsGenericType) continue;

                    var def = iface.GetGenericTypeDefinition();
                    if (def == typeof(IBeforeSaveTrigger<>)
                        || def == typeof(IAfterSaveTrigger<>)
                        || def == typeof(IAfterSaveFailedTrigger<>))
                    {
                        services.AddScoped(iface, implType);
                    }
                }
            }
        }

        return services;
    }
}
