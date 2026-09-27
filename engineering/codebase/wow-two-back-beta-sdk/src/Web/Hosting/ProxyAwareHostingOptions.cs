namespace WoW.Two.Sdk.Backend.Beta.Web.Hosting;

/// <summary>Holds options for proxy-aware hosting.</summary>
public sealed record ProxyAwareHostingOptions
{
    /// <summary>Host-header allowlist (host filtering). Empty (default) = allow any host; set to lock the app to known hostnames and reject Host-header spoofing.</summary>
    public IList<string> AllowedHosts { get; } = [];

    /// <summary>Gets the proxy IP addresses whose forwarded headers are applied, besides loopback.</summary>
    /// <remarks>A request from any other sender keeps its socket address, scheme and host.</remarks>
    public IList<string> TrustedProxies { get; } = [];

    /// <summary>Gets the proxy networks, in CIDR notation, whose forwarded headers are applied.</summary>
    public IList<string> TrustedNetworks { get; } = [];

    /// <summary>Gets or sets how many trusted proxy hops are unwound from forwarded headers. Defaults to one.</summary>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>Gets the hosts appended to a restricted host allowlist so local health probes pass. Defaults to <c>localhost</c>.</summary>
    /// <remarks>Applies to allowlists from options and from the <c>AllowedHosts</c> setting; a wildcard list is left unchanged.</remarks>
    public IList<string> ProbeHosts { get; } = ["localhost"];
}
