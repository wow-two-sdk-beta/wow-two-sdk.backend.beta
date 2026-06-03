using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.Versioning;

/// <summary>
/// API versioning registration — accepts version via URL segment, header, or query.
/// </summary>
public static class VersioningServiceCollectionExtensions
{
    /// <summary>Default version when none specified.</summary>
    public static readonly ApiVersion DefaultVersion = new(1, 0);

    /// <summary>
    /// Register API versioning + ApiExplorer. Default version: `1.0`. Sources: URL segment (e.g. `/v1/...`), header `api-version`, query `api-version=1.0`.
    /// </summary>
    public static IServiceCollection AddDefaultApiVersioning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = DefaultVersion;
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("api-version"),
                new QueryStringApiVersionReader("api-version"));
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}
