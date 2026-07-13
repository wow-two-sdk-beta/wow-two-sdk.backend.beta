using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Localization.Humanizing;

namespace WoW.Two.Sdk.Backend.Beta.Localization;

/// <summary>Registration helpers for the Localization vector — resx localization, request-culture conventions, and humanizing.</summary>
public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core localization backed by <c>.resx</c> resources under <paramref name="resourcesPath"/>,
    /// enabling injection of <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="resourcesPath">The project-relative folder holding <c>.resx</c> files. Default <c>"Resources"</c>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddResxLocalization(this IServiceCollection services, string resourcesPath = "Resources")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcesPath);

        services.AddLocalization(options => options.ResourcesPath = resourcesPath);
        return services;
    }

    /// <summary>
    /// Configures request localization from <see cref="LocalizationConventionOptions"/>: supported/default
    /// cultures and the query-string → cookie → <c>Accept-Language</c> provider chain. Apply the middleware
    /// with <see cref="RequestLocalizationBuilderExtensions.UseRequestLocalizationConventions"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Callback that must add at least one <see cref="LocalizationConventionOptions.SupportedCultures"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentException">No supported cultures were supplied.</exception>
    public static IServiceCollection AddRequestLocalizationConventions(this IServiceCollection services, Action<LocalizationConventionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var conventions = new LocalizationConventionOptions();
        configure(conventions);

        if (conventions.SupportedCultures.Count == 0)
            throw new ArgumentException("At least one supported culture must be configured.", nameof(configure));

        var cultures = conventions.SupportedCultures.Select(name => new CultureInfo(name)).ToArray();

        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(conventions.DefaultCulture);
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.ApplyCurrentCultureToResponseHeaders = true;

            options.RequestCultureProviders.Clear();
            if (conventions.EnableQueryStringProvider) options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
            if (conventions.EnableCookieProvider) options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
            if (conventions.EnableAcceptLanguageProvider) options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
        });

        return services;
    }

    /// <summary>
    /// Registers the humanizing services as singletons: <see cref="IRelativeTimeFormatter"/> (TimeProvider-backed
    /// "3 hours ago" phrasing) and <see cref="ITextHumanizer"/> (ordinals, quantities, pluralization). Both
    /// honour the ambient request culture set by the localization middleware. Idempotent.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddHumanizing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRelativeTimeFormatter, RelativeTimeFormatter>();
        services.TryAddSingleton<ITextHumanizer, TextHumanizer>();
        return services;
    }
}
