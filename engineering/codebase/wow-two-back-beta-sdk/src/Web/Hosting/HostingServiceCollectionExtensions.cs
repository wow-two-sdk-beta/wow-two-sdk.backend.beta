using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Web.RequestLimits;

namespace WoW.Two.Sdk.Backend.Beta.Web.Hosting;

/// <summary>Options for proxy-aware hosting.</summary>
public sealed class ProxyAwareHostingOptions
{
    /// <summary>Host-header allowlist (host filtering). Empty (default) = allow any host; set to lock the app to known hostnames and reject Host-header spoofing.</summary>
    public IList<string> AllowedHosts { get; } = [];
}

/// <summary>Provides proxy-aware hosting defaults: forwarded headers, host filtering, bounded request limits, and request decompression.</summary>
public static class HostingServiceCollectionExtensions
{
    /// <summary>
    /// Configures forwarded headers, optional host filtering (when <see cref="ProxyAwareHostingOptions.AllowedHosts"/> is
    /// set), bounded request limits (so the request decompression below is never unbounded), and request decompression.
    /// Pair with <see cref="UseProxyAwareHosting"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional hosting options (e.g. the host allowlist).</param>
    public static IServiceCollection AddProxyAwareHosting(this IServiceCollection services, Action<ProxyAwareHostingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ProxyAwareHostingOptions();
        configure?.Invoke(options);

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        if (options.AllowedHosts.Count > 0)
        {
            services.Configure<HostFilteringOptions>(o =>
            {
                o.AllowedHosts = [.. options.AllowedHosts];
                o.AllowEmptyHosts = false;
            });
        }

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
}
