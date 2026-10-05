using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;

namespace WoW.Two.Sdk.Backend.Beta.Ai.Mcp;

/// <summary>Registers stateless MCP hosting with per-operation authorization.</summary>
public static class McpServiceCollectionExtensions
{
    /// <summary>Registers the HTTP transport and authorization filters for caller-owned MCP tools.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The MCP builder for explicit tool, prompt and resource registration.</returns>
    public static IMcpServerBuilder AddStatelessMcpServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.SessionMode = HttpServerSessionMode.Stateless;
            })
            .AddAuthorizationFilters();
    }
}
