using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.Compression;

/// <summary>Provides Brotli and Gzip response compression at <c>Fastest</c> level.</summary>
public static class CompressionServiceCollectionExtensions
{
    /// <summary>Registers response compression. Pair with <c>app.UseResponseCompression()</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddBrotliGzipCompression(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

        return services;
    }
}
