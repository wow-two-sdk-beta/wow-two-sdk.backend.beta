using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Provides registration for the error-to-HTTP-status mapping seam.</summary>
public static class ErrorMappingServiceCollectionExtensions
{
    /// <summary>Registers the default <see cref="IErrorHttpStatusCodeMapper"/>, <see cref="IErrorMessageMapper"/> and <see cref="IFieldErrorMessageMapper"/>; apps register their own first to override.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddErrorHttpStatusMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IErrorHttpStatusCodeMapper, ErrorHttpStatusCodeMapper>();
        services.TryAddSingleton<IErrorMessageMapper, ErrorMessageMapper>();
        services.TryAddSingleton<IFieldErrorMessageMapper, FieldErrorMessageMapper>();

        return services;
    }
}
