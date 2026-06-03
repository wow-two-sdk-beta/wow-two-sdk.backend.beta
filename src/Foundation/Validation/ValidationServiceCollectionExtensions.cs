using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Validation;

/// <summary>
/// Registration helpers for FluentValidation + DataAnnotations bridge.
/// </summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers all <see cref="IValidator{T}"/> implementations from the calling assembly.
    /// </summary>
    public static IServiceCollection AddFluentValidatorsFromAssemblies(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddValidatorsFromAssembly(Assembly.GetCallingAssembly(), includeInternalTypes: true);
        return services;
    }

    /// <summary>
    /// Registers all <see cref="IValidator{T}"/> implementations from the supplied assemblies.
    /// </summary>
    public static IServiceCollection AddFluentValidatorsFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
