using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.Versioning;

/// <summary>Provides API versioning that accepts the version via URL segment, header, or query.</summary>
public static class VersioningServiceCollectionExtensions
{
    /// <summary>Gets the default version when none is specified.</summary>
    public static readonly ApiVersion DefaultVersion = new(1, 0);

    /// <summary>Adds API versioning and ApiExplorer (default <c>1.0</c>; sources: URL segment, header <c>api-version</c>, query <c>api-version</c>).</summary>
    /// <param name="services">The service collection to configure.</param>
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
