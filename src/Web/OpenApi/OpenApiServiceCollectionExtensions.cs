using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.OpenApi;

/// <summary>
/// `Microsoft.AspNetCore.OpenApi` registration. .NET 9 first-party path.
/// </summary>
public static class OpenApiServiceCollectionExtensions
{
    /// <summary>
    /// Register OpenAPI document generator. Pair with <see cref="MapOpenApiEndpoint"/> after build.
    /// </summary>
    public static IServiceCollection AddOpenApiDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOpenApi();
        return services;
    }

    /// <summary>
    /// Map the OpenAPI endpoint at `/openapi/{documentName}.json` (default).
    /// </summary>
    public static IEndpointRouteBuilder MapOpenApiEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapOpenApi();
        return endpoints;
    }
}
