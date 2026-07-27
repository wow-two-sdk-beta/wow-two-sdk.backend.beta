using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines a single exception-translation rule — recognizes a family of exceptions and maps them to an <see cref="AppError"/>, returning <see langword="null"/> to defer to the next rule.</summary>
public interface IExceptionMappingRule
{
    /// <summary>Maps <paramref name="exception"/> to an <see cref="AppError"/>, or returns <see langword="null"/> when this rule does not recognize it.</summary>
    /// <param name="exception">The caught exception.</param>
    AppError? TryMap(Exception exception);
}

/// <summary>Defines the seam that translates any caught exception into an <see cref="AppError"/> — the single mapper the mediator behavior and global handlers depend on.</summary>
public interface IExceptionMapper
{
    /// <summary>Maps <paramref name="exception"/> to an <see cref="AppError"/>, always returning a value (falling back to <see cref="AppErrorType.Unexpected"/>).</summary>
    /// <param name="exception">The caught exception.</param>
    AppError Map(Exception exception);
}

/// <summary>Provides the default exception mapping: unwraps a carried <see cref="AppException"/>, then applies the registered <see cref="IExceptionMappingRule"/> contributors (last-registered first, so an app rule shadows an SDK rule), then falls back to <see cref="AppErrorType.Unexpected"/>.</summary>
public sealed class ExceptionMapper : IExceptionMapper
{
    private readonly IExceptionMappingRule[] _rules;

    /// <summary>Initializes the mapper from the registered rules; later registrations take precedence.</summary>
    /// <param name="rules">The registered mapping rules, in registration order.</param>
    public ExceptionMapper(IEnumerable<IExceptionMappingRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules as IExceptionMappingRule[] ?? [.. rules];
    }

    /// <inheritdoc/>
    public AppError Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is AppException appException)
        {
            return appException.Error;
        }

        // Last-registered rule wins, so an app-registered rule shadows an SDK rule for the same exception.
        for (var index = _rules.Length - 1; index >= 0; index--)
        {
            var error = _rules[index].TryMap(exception);
            if (error is not null)
            {
                return error;
            }
        }

        return AppErrors.Unexpected(inner: exception);
    }
}

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
