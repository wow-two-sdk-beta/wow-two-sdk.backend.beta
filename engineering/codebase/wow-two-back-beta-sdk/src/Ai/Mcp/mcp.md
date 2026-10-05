# MCP

Opt-in, authenticated, stateless Streamable HTTP hosting for product-owned MCP tools.

```csharp
services.AddStatelessMcpServer().WithTools<ProductTools>();
app.MapAuthenticatedMcp("/mcp", "IntegrationKey");
```

- [Contract](Mcp.standard.md) · [API and wiring](Mcp.spec.md)
- Tool policies use the host's authorization registrations.
- No tool, scope, identity provider or network exposure is enabled implicitly.
