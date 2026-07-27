using Microsoft.AspNetCore.Builder;

namespace WoW.Two.Sdk.Backend.Beta.Localization;

/// <summary>Applies the request-localization middleware configured by <see cref="LocalizationServiceCollectionExtensions.AddRequestLocalizationConventions"/>.</summary>
public static class RequestLocalizationBuilderExtensions
{
    /// <summary>
    /// Adds the request-localization middleware using the options registered by
    /// <c>AddRequestLocalizationConventions</c>. Place it early in the pipeline so downstream components see
    /// the resolved culture.
    /// </summary>
    /// <param name="app">The application pipeline to extend.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
    public static IApplicationBuilder UseRequestLocalizationConventions(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseRequestLocalization();
    }
}
