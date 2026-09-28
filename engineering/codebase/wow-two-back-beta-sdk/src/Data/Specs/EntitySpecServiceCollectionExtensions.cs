using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Entity spec registration.</summary>
public static class EntitySpecServiceCollectionExtensions
{
    /// <summary>
    /// Registers entity specs: every <see cref="IEntitySpecConfiguration{TEntity}"/> in <paramref name="assemblies"/> joins
    /// the one <see cref="EntitySpecRegistry"/>, which <c>AppDbContextBase</c> maps into its EF Core model.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assemblies">The assemblies holding spec configurations.</param>
    public static IServiceCollection AddEntitySpecs(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        RegistryOf(services).AddFromAssemblies(assemblies);
        return services;
    }

    /// <summary>
    /// Registers entity specs in code. Options come from <paramref name="configure"/>, then the host section
    /// <c>Data:Specs</c>, so a host can switch unsupported features from throwing to skipping without a rebuild.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="register">Adds specs to the registry.</param>
    /// <param name="configure">How mappers treat unsupported features.</param>
    public static IServiceCollection AddEntitySpecs(this IServiceCollection services, Action<EntitySpecRegistry> register, Action<EntitySpecOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(register);
        register(RegistryOf(services));
        services.AddModuleOptions(EntitySpecOptions.SectionName, configure);
        return services;
    }

    /// <summary>The one registry of the collection, registered on first use.</summary>
    private static EntitySpecRegistry RegistryOf(IServiceCollection services)
    {
        if (services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(EntitySpecRegistry))?.ImplementationInstance is EntitySpecRegistry registry)
            return registry;

        registry = new EntitySpecRegistry();
        services.AddSingleton(registry);
        services.AddModuleOptions<EntitySpecOptions>(EntitySpecOptions.SectionName, configure: null);
        return registry;
    }
}
