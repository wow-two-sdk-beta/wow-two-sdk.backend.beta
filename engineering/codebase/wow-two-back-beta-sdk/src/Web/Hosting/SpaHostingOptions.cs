namespace WoW.Two.Sdk.Backend.Beta.Web.Hosting;

/// <summary>Holds configuration for <c>MapSpaFallback</c> / <c>UseSpaHosting</c> when serving a single-page app from the same host as the API.</summary>
public sealed record SpaHostingOptions
{
    /// <summary>
    /// Route prefix reserved for API endpoints. Requests under this prefix that match no endpoint return a JSON 404
    /// instead of the SPA shell, so clients never cache an HTML body for an API path. Default <c>/api</c>.
    /// </summary>
    public string ApiPathPrefix { get; set; } = "/api";

    /// <summary>Static file served for unmatched non-API routes, relative to the web root. Default <c>index.html</c>.</summary>
    public string FallbackFile { get; set; } = "index.html";

    /// <summary>
    /// Serve a default document (for example <c>index.html</c>) when a directory is requested, mapping the request path to
    /// the default file before static-file resolution. Default <see langword="true"/>.
    /// </summary>
    public bool ServeDefaultFiles { get; set; } = true;

    /// <summary>
    /// Redirect a route folder requested without its trailing slash (<c>/pricing</c>) to the slashed path
    /// (<c>/pricing/</c>) before serving its default document. Default <see langword="false"/>: a SPA bundle references
    /// assets from the root, and a prerendered route keeps the slashless address its router and canonical links use.
    /// Set it for a folder whose document loads assets relative to itself.
    /// </summary>
    public bool RedirectToTrailingSlash { get; set; }
}
