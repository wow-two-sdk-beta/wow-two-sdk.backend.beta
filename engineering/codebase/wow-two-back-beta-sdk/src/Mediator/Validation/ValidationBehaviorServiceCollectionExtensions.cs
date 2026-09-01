using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Validation;

/// <summary>Provides registration for the validation pipeline behavior.</summary>
public static class ValidationBehaviorServiceCollectionExtensions
{
    /// <summary>Registers the validation pipeline behavior.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMediatorValidatingInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddMediatorInterceptor(typeof(ValidatingInterceptor<,>));
    }
}
