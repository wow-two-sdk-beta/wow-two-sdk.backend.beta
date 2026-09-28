using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Provides registration for FluentValidation-backed validators behind the <see cref="IValidator{T}"/> wrapper.</summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the validation module's <see cref="ValidationOptions"/>: defaults, then <paramref name="configure"/>, then the
    /// host section <c>Validation</c> (for example <c>Validation:Translation:Enabled</c>), host configuration last.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Code configuration; call again to add more.</param>
    public static IServiceCollection ConfigureValidation(this IServiceCollection services, Action<ValidationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddModuleOptions(
            ValidationOptions.SectionName,
            configure,
            builder => builder.Validate(
                options => !string.IsNullOrWhiteSpace(options.Translation.DefaultCulture),
                "ValidationOptions.Translation.DefaultCulture must not be empty."));
    }

    /// <summary>Registers FluentValidation validators from the calling assembly behind the <see cref="IValidator{T}"/> wrapper.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddFluentValidatorsFromAssemblies(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.ConfigureValidation();
        services.AddValidatorsFromAssembly(Assembly.GetCallingAssembly(), includeInternalTypes: true);
        services.TryAddTransient(typeof(IValidator<>), typeof(FluentValidationAdapter<>));
        return services;
    }

    /// <summary>Registers FluentValidation validators from the supplied assemblies behind the <see cref="IValidator{T}"/> wrapper.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assemblies">The assemblies to scan for validators.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddFluentValidatorsFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);
        services.ConfigureValidation();
        foreach (var assembly in assemblies)
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.TryAddTransient(typeof(IValidator<>), typeof(FluentValidationAdapter<>));
        return services;
    }
}
