using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;

/// <summary>Provides registration for mapping thrown exceptions to ProblemDetails responses.</summary>
public static class ExceptionHandlingServiceCollectionExtensions
{
    /// <summary>Adds the handler mapping a <see cref="ValidationException"/> to a 400 ProblemDetails response.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddValidationExceptionHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddExceptionHandler<ValidationExceptionHandler>();
        return services;
    }
}
