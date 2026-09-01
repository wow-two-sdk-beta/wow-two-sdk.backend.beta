namespace WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders;

/// <summary>Holds the cross-origin isolation policies the OWASP secure-headers preset emits.</summary>
public sealed record SecureHeadersOptions
{
    /// <summary>Gets or sets whether a response carries <c>Cross-Origin-Opener-Policy: same-origin</c>, which severs the <c>window.opener</c> handle of a cross-origin popup.</summary>
    public bool EnableCrossOriginOpenerPolicy { get; set; } = true;

    /// <summary>Gets or sets whether a response carries <c>Cross-Origin-Embedder-Policy: require-corp</c>, which blocks a cross-origin subresource that ships no CORP opt-in.</summary>
    public bool EnableCrossOriginEmbedderPolicy { get; set; } = true;
}
