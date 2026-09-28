namespace WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders;

/// <summary>Holds the cross-origin isolation policies the OWASP secure-headers preset emits.</summary>
public sealed record SecureHeadersOptions
{
    /// <summary>Gets or sets whether a response carries <c>Cross-Origin-Opener-Policy: same-origin</c>, which severs the <c>window.opener</c> handle of a cross-origin popup.</summary>
    public bool EnableCrossOriginOpenerPolicy { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the opener policy is <c>same-origin-allow-popups</c> instead of <c>same-origin</c>, which keeps the
    /// <c>window.opener</c> of popups this origin opens — OAuth popups such as Google Identity Services need it. Default off.
    /// </summary>
    public bool AllowOpenerPopups { get; set; }

    /// <summary>Gets or sets whether a response carries <c>Cross-Origin-Embedder-Policy: require-corp</c>, which blocks a cross-origin subresource that ships no CORP opt-in.</summary>
    public bool EnableCrossOriginEmbedderPolicy { get; set; } = true;
}
