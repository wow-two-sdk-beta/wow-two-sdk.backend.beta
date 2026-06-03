using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.Hosting;

/// <summary>
/// Conventional hosting defaults: forwarded headers (so `X-Forwarded-For` etc. are honored behind a proxy),
/// request decompression, and a polite default for host filtering.
/// </summary>
public static class HostingServiceCollectionExtensions
{
    /// <summary>
    /// Configure forwarded-headers + request decompression. Pair with <see cref="UseProxyAwareHosting"/> in the pipeline.
    /// </summary>
    public static IServiceCollection AddProxyAwareHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
        });

        services.AddRequestDecompression();
        return services;
    }

    /// <summary>
    /// Pipeline counterpart: invoke forwarded headers + request decompression.
    /// Call early in the pipeline.
    /// </summary>
    public static IApplicationBuilder UseProxyAwareHosting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseForwardedHeaders();
        app.UseRequestDecompression();
        return app;
    }
}
