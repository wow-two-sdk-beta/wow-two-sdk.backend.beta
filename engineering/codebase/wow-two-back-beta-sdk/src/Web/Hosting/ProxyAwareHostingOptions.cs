using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Web.RequestLimits;

namespace WoW.Two.Sdk.Backend.Beta.Web.Hosting;

/// <summary>Options for proxy-aware hosting.</summary>
public sealed record ProxyAwareHostingOptions
{
    /// <summary>Host-header allowlist (host filtering). Empty (default) = allow any host; set to lock the app to known hostnames and reject Host-header spoofing.</summary>
    public IList<string> AllowedHosts { get; } = [];
}
