namespace WoW.Two.Sdk.Backend.Beta.Web.RequestLimits;

/// <summary>
/// Server request-size and header-timing limits. Bounds otherwise-unbounded request input — including the compressed
/// body a request-decompression handler reads (a decompression-bomb vector) — plus header size and slow-header attacks.
/// </summary>
public sealed class RequestLimitsOptions
{
    /// <summary>Max request body size in bytes; caps the (compressed) input a decompression handler will read. Default 30 MB. <c>null</c> = no limit (not recommended).</summary>
    public long? MaxRequestBodySizeBytes { get; set; } = 30L * 1024 * 1024;

    /// <summary>Max total size of all request headers in bytes. Default 32 KB.</summary>
    public int MaxRequestHeadersTotalSizeBytes { get; set; } = 32 * 1024;

    /// <summary>Max size of the request line (method + URL + version) in bytes. Default 8 KB.</summary>
    public int MaxRequestLineSizeBytes { get; set; } = 8 * 1024;

    /// <summary>Time the server waits for the complete request headers before aborting the connection (slow-loris guard). Default 30s.</summary>
    public TimeSpan RequestHeadersTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
