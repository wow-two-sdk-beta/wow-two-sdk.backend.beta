using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Provides registration for the error-to-HTTP-status mapping seam.</summary>
public static class ErrorMappingServiceCollectionExtensions
{
    /// <summary>Registers the default <see cref="IErrorHttpStatusCodeMapper"/> and <see cref="IErrorMessageResolver"/>; apps register their own first to override.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddErrorHttpStatusMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IErrorHttpStatusCodeMapper, DefaultErrorHttpStatusCodeMapper>();
        services.TryAddSingleton<IErrorMessageResolver, DefaultErrorMessageResolver>();

        return services;
    }
}
