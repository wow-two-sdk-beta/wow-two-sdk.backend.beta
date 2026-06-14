using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Provides registration for FluentValidation-backed validators behind the <see cref="IValidator{T}"/> wrapper.</summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>Registers FluentValidation validators from the calling assembly behind the <see cref="IValidator{T}"/> wrapper.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddFluentValidatorsFromAssemblies(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddValidatorsFromAssembly(Assembly.GetCallingAssembly(), includeInternalTypes: true);
        services.TryAddTransient(typeof(IValidator<>), typeof(FluentValidationAdapter<>));
        return services;
    }

    /// <summary>Registers FluentValidation validators from the supplied assemblies behind the <see cref="IValidator{T}"/> wrapper.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan for validators.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddFluentValidatorsFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);
        foreach (var assembly in assemblies)
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.TryAddTransient(typeof(IValidator<>), typeof(FluentValidationAdapter<>));
        return services;
    }
}
