using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Provides registration for the exception-to-<see cref="AppError"/> mapping seam.</summary>
public static class ExceptionMappingServiceCollectionExtensions
{
    /// <summary>Registers the default <see cref="IExceptionMapper"/>; an app registers its own first to replace the facade wholesale.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddExceptionMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IExceptionMapper, ExceptionMapper>();

        return services;
    }

    /// <summary>Registers an additional <see cref="IExceptionMappingRule"/> contributor; the most-recently-added rule wins for a given exception. Use this to map exceptions the SDK does not know.</summary>
    /// <typeparam name="TRule">The rule implementation.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddExceptionMappingRule<TRule>(this IServiceCollection services)
        where TRule : class, IExceptionMappingRule
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExceptionMapping();
        services.AddSingleton<IExceptionMappingRule, TRule>();

        return services;
    }

    /// <summary>Registers a singleton <see cref="IExceptionMappingRule"/> instance; the most-recently-added rule wins for a given exception.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="rule">The rule instance to register.</param>
    public static IServiceCollection AddExceptionMappingRule(this IServiceCollection services, IExceptionMappingRule rule)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(rule);

        services.AddExceptionMapping();
        services.AddSingleton<IExceptionMappingRule>(rule);

        return services;
    }
}
