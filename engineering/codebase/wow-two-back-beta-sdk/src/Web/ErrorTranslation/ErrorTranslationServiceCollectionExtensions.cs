using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorTranslation;

/// <summary>Registers error translation over the validation module's options.</summary>
public static class ErrorTranslationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IErrorTranslationService"/>, reading <see cref="ValidationOptions.Translation"/> live. Enable it with
    /// <c>ConfigureValidation(o => o.Translation.Enabled = true)</c> or <c>Validation:Translation:Enabled</c> in host configuration;
    /// the error-mapping registration calls this already.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddErrorTranslation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.ConfigureValidation();
        services.TryAddSingleton<IErrorTranslationService, ErrorTranslationService>();
        return services;
    }
}
