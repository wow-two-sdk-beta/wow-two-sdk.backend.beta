using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Web.OpenApi;

/// <summary>Provides <c>Microsoft.AspNetCore.OpenApi</c> registration (.NET 9 first-party path).</summary>
public static class OpenApiServiceCollectionExtensions
{
    /// <summary>Adds the OpenAPI document generator. Pair with <see cref="MapOpenApiEndpoint"/> after build.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddOpenApiDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOpenApi();
        return services;
    }

    /// <summary>Maps the OpenAPI endpoint at <c>/openapi/{documentName}.json</c> (default).</summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    public static IEndpointRouteBuilder MapOpenApiEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapOpenApi();
        return endpoints;
    }
}
