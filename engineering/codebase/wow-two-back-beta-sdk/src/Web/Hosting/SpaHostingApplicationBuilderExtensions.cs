using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

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
    /// <param name="configure">
    /// Optional configuration; <see cref="SpaHostingOptions.ServeDefaultFiles"/> and
    /// <see cref="SpaHostingOptions.RedirectToTrailingSlash"/> are read here.
    /// </param>
    /// <returns>The same <paramref name="app"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseSpaHosting(this IApplicationBuilder app, Action<SpaHostingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = BuildOptions(configure);

        if (options.ServeDefaultFiles)
        {
            // Before routing, a route folder (pricing/index.html) answers /pricing itself; the fallback covers the rest.
            app.UseDefaultFiles(new DefaultFilesOptions { RedirectToAppendTrailingSlash = options.RedirectToTrailingSlash });
        }

        app.UseStaticFiles(StaticFiles(options));
        return app;
    }

    /// <summary>
    /// Maps the SPA fallback terminal endpoints: a JSON 404 for unmatched routes under
    /// <see cref="SpaHostingOptions.ApiPathPrefix"/>, then the route's own document for every other unmatched route —
    /// <c>pricing/index.html</c> for <c>/pricing</c> when the bundle ships one — else
    /// <see cref="SpaHostingOptions.FallbackFile"/>. Call after the application's own endpoints are mapped so real routes
    /// win; both fallbacks allow anonymous access so the shell and 404 surface under a default-deny authorization policy.
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

        // Terminate unmatched API routes before the SPA catch-all can return HTML.
        app.MapFallback(apiPattern, () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found"))
            .AllowAnonymous();

        // Routing claims extensionless paths before static files run, so the fallback itself picks the document.
        var webRoot = app.Environment.WebRootFileProvider;
        var shellPath = $"/{options.FallbackFile.TrimStart('/')}";
        var serveDocument = ((IEndpointRouteBuilder)app).CreateApplicationBuilder()
            .Use(next => context =>
            {
                context.Request.Path = RouteDocumentPath(webRoot, context.Request.Path) ?? shellPath;
                context.SetEndpoint(null);
                return next(context);
            })
            .UseStaticFiles(StaticFiles(options))
            .Build();
        app.MapFallback(serveDocument)
            .AllowAnonymous();

        return app;
    }

    /// <summary>Builds the static-file options that cache hashed assets for a year and make every document revalidate.</summary>
    /// <param name="options">The SPA hosting options.</param>
    private static StaticFileOptions StaticFiles(SpaHostingOptions options)
    {
        var immutable = string.IsNullOrWhiteSpace(options.ImmutableAssetsPath)
            ? PathString.Empty
            : new PathString($"/{options.ImmutableAssetsPath.Trim('/')}");
        return new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                var headers = context.Context.Response.Headers;
                if (immutable.HasValue && context.Context.Request.Path.StartsWithSegments(immutable))
                    headers.CacheControl = "public, max-age=31536000, immutable";
                else if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    headers.CacheControl = "no-cache";
            },
        };
    }

    /// <summary>Returns the path of the route's prerendered <c>index.html</c>, or null when the bundle has none.</summary>
    /// <param name="webRoot">The web root the bundle is served from.</param>
    /// <param name="path">The request path.</param>
    private static string? RouteDocumentPath(IFileProvider webRoot, PathString path)
    {
        var route = path.Value?.Trim('/');
        if (string.IsNullOrEmpty(route))
            return null;

        // The provider refuses paths that climb out of the web root, so a crafted path finds nothing.
        var document = $"/{route}/index.html";
        return webRoot.GetFileInfo(document) is { Exists: true, IsDirectory: false } ? document : null;
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
