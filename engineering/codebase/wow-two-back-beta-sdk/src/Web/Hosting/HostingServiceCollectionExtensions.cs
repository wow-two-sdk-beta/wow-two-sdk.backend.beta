using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Web.RequestLimits;
using IPNetwork = System.Net.IPNetwork;

namespace WoW.Two.Sdk.Backend.Beta.Web.Hosting;

/// <summary>Provides proxy-aware hosting defaults: forwarded headers, host filtering, bounded request limits, and request decompression.</summary>
public static class HostingServiceCollectionExtensions
{
    /// <summary>
    /// Configures forwarded headers from trusted proxies, optional host filtering (when <see cref="ProxyAwareHostingOptions.AllowedHosts"/> is
    /// set), bounded request limits (so the request decompression below is never unbounded), and request decompression.
    /// Pair with <see cref="UseProxyAwareHosting"/>.
    /// </summary>
    /// <remarks>
    ///   - forwarded headers apply only from loopback and the configured proxies or networks; others cannot spoof a client address
    ///   - probe hosts join any restricted allowlist, so a loopback health check passes host filtering
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional hosting options (e.g. the host allowlist).</param>
    /// <exception cref="ArgumentException">A trusted proxy or network is not a valid address or CIDR range.</exception>
    public static IServiceCollection AddProxyAwareHosting(this IServiceCollection services, Action<ProxyAwareHostingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new ProxyAwareHostingOptions();
        configure?.Invoke(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ForwardLimit, 1, nameof(configure));
        IPAddress[] proxies = [.. options.TrustedProxies.Select(ParseProxy)];
        IPNetwork[] networks = [.. options.TrustedNetworks.Select(ParseNetwork)];
        string[] probes = [.. options.ProbeHosts];

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.ForwardLimit = options.ForwardLimit;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
            o.KnownProxies.Add(IPAddress.Loopback);
            o.KnownProxies.Add(IPAddress.IPv6Loopback);
            foreach (IPAddress proxy in proxies)
                o.KnownProxies.Add(proxy);
            foreach (IPNetwork network in networks)
                o.KnownIPNetworks.Add(network);
        });
        if (options.AllowedHosts.Count > 0)
        {
            services.Configure<HostFilteringOptions>(o =>
            {
                o.AllowedHosts = [.. options.AllowedHosts];
                o.AllowEmptyHosts = false;
            });
        }

        services.PostConfigure<HostFilteringOptions>(o => AppendProbeHosts(o, probes));
        services.AddRequestLimits();       // bound the (compressed) input UseRequestDecompression will read
        services.AddRequestDecompression();
        return services;
    }

    /// <summary>Uses forwarded headers, host filtering, and request decompression. Call early in the pipeline.</summary>
    /// <param name="app">The application request pipeline.</param>
    public static IApplicationBuilder UseProxyAwareHosting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseForwardedHeaders();
        app.UseHostFiltering();            // honors HostFilteringOptions.AllowedHosts (unconfigured = allow any)
        app.UseRequestDecompression();
        return app;
    }

    private static void AppendProbeHosts(HostFilteringOptions options, string[] probes)
    {
        if (options.AllowedHosts is not { Count: > 0 } hosts || hosts.Contains("*"))
        {
            return;
        }

        foreach (string probe in probes)
        {
            if (!hosts.Contains(probe, StringComparer.OrdinalIgnoreCase))
                hosts.Add(probe);
        }
    }

    private static IPAddress ParseProxy(string value) =>
        IPAddress.TryParse(value, out IPAddress? address)
            ? address
            : throw new ArgumentException($"Trusted proxy '{value}' is not an IP address.", nameof(value));

    private static IPNetwork ParseNetwork(string value) =>
        IPNetwork.TryParse(value, out IPNetwork network)
            ? network
            : throw new ArgumentException($"Trusted network '{value}' is not a CIDR range.", nameof(value));
}
