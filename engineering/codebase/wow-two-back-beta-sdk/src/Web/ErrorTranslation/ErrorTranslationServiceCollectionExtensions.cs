using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorTranslation;

/// <summary>Registers config-activated error translation.</summary>
public static class ErrorTranslationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IErrorTranslationService"/> with <see cref="ErrorTranslationSettings"/> bound from the container's
    /// <see cref="IConfiguration"/> section <c>ErrorTranslation</c>, reloading with it. Without the section, or with
    /// <c>Enabled</c> false, every message stays as authored. The error-mapping registration calls this already.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddErrorTranslation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IErrorTranslationService)))
            return services;

        services.AddOptions<ErrorTranslationSettings>()
            .Configure<IServiceProvider>((settings, provider) =>
                provider.GetService<IConfiguration>()?.GetSection(ErrorTranslationSettings.SectionName).Bind(settings));
        services.AddSingleton<IOptionsChangeTokenSource<ErrorTranslationSettings>>(provider =>
            new ConfigurationChangeTokenSource<ErrorTranslationSettings>(
                (IConfiguration?)provider.GetService<IConfiguration>()?.GetSection(ErrorTranslationSettings.SectionName)
                    ?? new ConfigurationBuilder().Build()));
        services.TryAddSingleton<IErrorTranslationService, ErrorTranslationService>();
        return services;
    }
}
