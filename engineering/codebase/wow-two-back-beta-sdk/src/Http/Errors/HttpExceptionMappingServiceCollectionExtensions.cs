using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Http.Errors;

/// <summary>Registers the outbound HTTP exception mapping.</summary>
public static class HttpExceptionMappingServiceCollectionExtensions
{
    /// <summary>Adds <see cref="HttpExceptionMappingRule"/> to the exception mapper; <c>AddApiDefaults</c> calls this already.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddHttpExceptionMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Any(descriptor => descriptor.ImplementationType == typeof(HttpExceptionMappingRule))
            ? services
            : services.AddExceptionMappingRule<HttpExceptionMappingRule>();
    }
}
