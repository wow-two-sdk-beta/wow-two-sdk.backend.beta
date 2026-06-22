using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.Hosting;

/// <summary>Provides proxy-aware hosting defaults: forwarded headers, request decompression, and host filtering.</summary>
public static class HostingServiceCollectionExtensions
{
    /// <summary>Configures forwarded headers and request decompression. Pair with <see cref="UseProxyAwareHosting"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddProxyAwareHosting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        services.AddRequestDecompression();
        return services;
    }

    /// <summary>Uses forwarded headers and request decompression. Call early in the pipeline.</summary>
    /// <param name="app">The application request pipeline.</param>
    public static IApplicationBuilder UseProxyAwareHosting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseForwardedHeaders();
        app.UseRequestDecompression();
        return app;
    }
}
