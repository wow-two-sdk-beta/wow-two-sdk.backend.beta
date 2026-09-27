namespace WoW.Two.Sdk.Backend.Beta.Web.Antiforgery;

/// <summary>Holds options for antiforgery protection of a single-page app that authenticates with cookies.</summary>
public sealed record SpaAntiforgeryOptions
{
    /// <summary>Gets or sets the script-readable cookie that carries the request token. Defaults to <c>XSRF-TOKEN</c>.</summary>
    public string CookieName { get; set; } = "XSRF-TOKEN";

    /// <summary>Gets or sets the header the client echoes the token in. Defaults to <c>X-XSRF-TOKEN</c>.</summary>
    public string HeaderName { get; set; } = "X-XSRF-TOKEN";

    /// <summary>Gets the path prefixes exempt from validation, such as signed provider callbacks.</summary>
    public IList<string> ExemptPathPrefixes { get; } = [];
}
