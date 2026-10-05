# MCP HTTP host

```csharp
services.AddStatelessMcpServer().WithTools<ProductTools>();
app.MapAuthenticatedMcp("/mcp", "IntegrationKey");
```

- `AddStatelessMcpServer` returns the official `IMcpServerBuilder` for explicit tool registration.
- `MapAuthenticatedMcp` requires a nonempty path and named policy; authentication middleware precedes it.
- Tool methods use the official `[McpServerTool]` and ASP.NET Core `[Authorize(Policy = "...")]` attributes.
- Authorization filters omit forbidden tools from discovery and deny direct invocation.
- Stateless requests use fresh authentication context and request-scoped dependencies.
- Existing `initialize` clients and current `server/discover` clients share the endpoint.
- MCP tools, resources, prompts and client capabilities follow the [official SDK](https://csharp.sdk.modelcontextprotocol.io/v2/).
