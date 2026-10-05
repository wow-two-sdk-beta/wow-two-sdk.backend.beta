using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace WoW.Two.Sdk.Backend.Beta.Ai.Mcp;

/// <summary>Maps MCP endpoints protected by a named host policy.</summary>
public static class McpEndpointRouteBuilderExtensions
{
    /// <summary>Maps the stateless MCP transport and requires the supplied authorization policy.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The MCP endpoint path.</param>
    /// <param name="policy">The registered authorization policy.</param>
    /// <returns>The endpoint builder for further explicit conventions.</returns>
    public static IEndpointConventionBuilder MapAuthenticatedMcp(
        this IEndpointRouteBuilder endpoints, string pattern, string policy)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        return endpoints.MapMcp(pattern).RequireAuthorization(policy);
    }
}
