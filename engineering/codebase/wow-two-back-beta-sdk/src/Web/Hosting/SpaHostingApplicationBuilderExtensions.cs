using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace WoW.Two.Sdk.Backend.Beta.Web.Hosting;

/// <summary>
/// Serves a single-page app from the same host as the API: static assets, a JSON-404 guard for unmatched API routes,
/// and a fallback to the SPA shell for client-routed paths.
/// </summary>
public static class SpaHostingApplicationBuilderExtensions
{
    /// <summary>
    /// Enables static-file serving for the SPA bundle (default document then static files). Call early in the pipeline,
    /// before authentication and endpoint mapping, so assets short-circuit and stay reachable without auth. Pair with
    /// <see cref="MapSpaFallback(WebApplication, Action{SpaHostingOptions}?)"/> after endpoints are mapped.
    /// </summary>
    /// <param name="app">The application request pipeline.</param>
    /// <param name="configure">Optional configuration; only <see cref="SpaHostingOptions.ServeDefaultFiles"/> is read here.</param>
    /// <returns>The same <paramref name="app"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseSpaHosting(this IApplicationBuilder app, Action<SpaHostingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = BuildOptions(configure);

        if (options.ServeDefaultFiles)
        {
            app.UseDefaultFiles();
        }

        app.UseStaticFiles();
        return app;
    }

    /// <summary>
    /// Maps the SPA fallback terminal endpoints: a JSON 404 for unmatched routes under
    /// <see cref="SpaHostingOptions.ApiPathPrefix"/>, then <see cref="SpaHostingOptions.FallbackFile"/> for every other
    /// unmatched route. Call after the application's own endpoints are mapped so real routes win; both fallbacks allow
    /// anonymous access so the shell and 404 surface under a default-deny authorization policy.
    /// </summary>
    /// <param name="app">The built application to map endpoints onto.</param>
    /// <param name="configure">Optional configuration for the API prefix and fallback file.</param>
    /// <returns>The same <paramref name="app"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static WebApplication MapSpaFallback(this WebApplication app, Action<SpaHostingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = BuildOptions(configure);
        var apiPattern = $"{options.ApiPathPrefix.TrimEnd('/')}/{{**slug}}";

        // An unmatched API route must 404 as JSON, never fall through to the SPA shell: an HTML body cached against an
        // API path breaks clients. This terminal route is ordered before the catch-all file fallback so it wins.
        app.MapFallback(apiPattern, () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found"))
            .AllowAnonymous();

        app.MapFallbackToFile(options.FallbackFile)
            .AllowAnonymous();

        return app;
    }

    /// <summary>Builds an <see cref="SpaHostingOptions"/> instance, applying the optional caller configuration.</summary>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The configured options.</returns>
    private static SpaHostingOptions BuildOptions(Action<SpaHostingOptions>? configure)
    {
        var options = new SpaHostingOptions();
        configure?.Invoke(options);
        return options;
    }
}
