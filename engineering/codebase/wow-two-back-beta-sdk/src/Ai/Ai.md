# Ai

*LLM chat + embeddings on the `Microsoft.Extensions.AI` seam, one uniform pipeline across provider brokers, plus token counting for cost/budget.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Ai`. `IChatClient` / `IEmbeddingGenerator` are the M.E.AI abstractions; the SDK adds conventional middleware + per-provider registration.

## Surface (wave 1)

| Folder | Surface | Role |
|---|---|---|
| `Core/` | `ChatExtensions.Apply`, `AiPipelineOptions` | Uniform pipeline (function-invocation + OpenTelemetry) every broker terminates with |
| `Ollama/` | `AddOllamaChatClient(endpoint, model)`, `AddOllamaEmbeddingGenerator(...)` | Local models via the Ollama server |
| `Tokenizers/` | `AddTiktokenTokenCounter(model)`, `ITokenCounter` | tiktoken token counting for prompt-budget / cost |
| `Mcp/` | `AddStatelessMcpServer`, `MapAuthenticatedMcp` | Explicit authenticated HTTP hosting for product tools |

## Quickstart

```csharp
builder.Services
    .AddOllamaChatClient("http://localhost:11434", "llama3.2")   // registers IChatClient (pipeline-wrapped)
    .AddOllamaEmbeddingGenerator(new Uri("http://localhost:11434"), "nomic-embed-text")
    .AddTiktokenTokenCounter("gpt-4o");

public sealed class Assistant(IChatClient chat, ITokenCounter tokens)
{
    public Task<ChatResponse> AskAsync(string prompt, CancellationToken ct)
    {
        _ = tokens.Count(prompt);                       // budget check
        return chat.GetResponseAsync(prompt, cancellationToken: ct);
    }
}
```

Every broker wraps its raw client with the SDK pipeline (`ChatExtensions.Apply`) so tool-calling and OTel are on by default (toggle via `AiPipelineOptions`).

## Provider status

| Provider | Status |
|---|---|
| **Ollama** (local) | shipped (wave 1) |
| Tokenizers (tiktoken) | shipped (wave 1) |
| **Anthropic · OpenAI · Azure OpenAI** | provider registrations pending; the shared stack uses the `GetResponseAsync` API |
| MCP | stateless authenticated HTTP host implemented; publication and broader vector completeness pending |
| Gemini · Bedrock · Llama · Semantic Kernel · vector stores | roadmap |

## Version note

`Microsoft.Extensions.AI 10.8.3`, OllamaSharp `5.5.0` and MCP `2.2.0` share the current AI abstractions.
`ChatExtensions.Apply` takes a builder that already owns its inner client and calls `Build()`;
the preview overload accepting a separate `innerClient` is removed.
