# MCP host contract

- `AddStatelessMcpServer` MUST use the official MCP C# SDK Streamable HTTP transport.
- Registration MUST enable authorization filters for tool, resource and prompt policies.
- The host MUST register its authentication, authorization policies and explicit tool types.
- `MapAuthenticatedMcp` MUST require the caller's named authorization policy.
- Transport MUST remain stateless and MUST disable legacy SSE endpoints.
- Registration MUST NOT scan assemblies, register product tools or expose an endpoint automatically.
- Tool implementations MUST preserve caller cancellation and MUST NOT expose unexpected exception details.
- Authentication and private-network deployment remain the host's responsibility.

Protocol: [official C# SDK](https://csharp.sdk.modelcontextprotocol.io/v2/).
