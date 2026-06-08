using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Provides registration for mapping thrown exceptions to ProblemDetails responses.</summary>
public static class ExceptionHandlingServiceCollectionExtensions
{
    /// <summary>Registers the handler that maps a <see cref="ValidationException"/> to a 400 ProblemDetails response.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddValidationExceptionHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddExceptionHandler<ValidationExceptionHandler>();
        return services;
    }
}
