using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.RequestLimits;

/// <summary>Server request-limit defaults that bound body size, header size, request-line size, and header-arrival time.</summary>
public static class RequestLimitsServiceCollectionExtensions
{
    /// <summary>
    /// Configure Kestrel request limits — max body size, header size, request-line size, and headers timeout — so
    /// request input (including the compressed body a decompression handler reads) is bounded rather than unbounded.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional overrides for the default limits.</param>
    public static IServiceCollection AddRequestLimits(this IServiceCollection services, Action<RequestLimitsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new RequestLimitsOptions();
        configure?.Invoke(options);

        services.Configure<KestrelServerOptions>(kestrel =>
        {
            kestrel.Limits.MaxRequestBodySize = options.MaxRequestBodySizeBytes;
            kestrel.Limits.MaxRequestHeadersTotalSize = options.MaxRequestHeadersTotalSizeBytes;
            kestrel.Limits.MaxRequestLineSize = options.MaxRequestLineSizeBytes;
            kestrel.Limits.RequestHeadersTimeout = options.RequestHeadersTimeout;
        });
        return services;
    }
}
