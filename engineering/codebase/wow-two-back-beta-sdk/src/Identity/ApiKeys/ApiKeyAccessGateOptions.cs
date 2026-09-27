using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Holds which paths the API key gate guards and who passes it without a key.</summary>
public sealed class ApiKeyAccessGateOptions
{
    /// <summary>Gets the path prefixes the gate guards; default <c>/api</c>.</summary>
    public IList<PathString> GuardedPaths { get; } = [new PathString("/api")];

    /// <summary>Gets the guarded path prefixes that stay open to everyone, such as a liveness probe.</summary>
    public IList<PathString> OpenPaths { get; } = [];

    /// <summary>Gets the guarded path prefixes a key may never reach, such as key management — this machine only.</summary>
    public IList<PathString> LocalOnlyPaths { get; } = [];

    /// <summary>Gets or sets whether a loopback caller passes without a key; default true.</summary>
    /// <remarks>
    /// Turn it off behind a reverse proxy on the same machine unless forwarded headers restore the client address —
    /// otherwise every proxied request looks local.
    /// </remarks>
    public bool AllowLocalWithoutKey { get; set; } = true;
}
