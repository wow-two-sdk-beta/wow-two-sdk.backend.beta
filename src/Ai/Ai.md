# Ai

*LLM chat + embeddings on the `Microsoft.Extensions.AI` seam, one uniform pipeline across provider brokers, plus token counting for cost/budget.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Ai`. `IChatClient` / `IEmbeddingGenerator` are the M.E.AI abstractions; the SDK adds conventional middleware + per-provider registration.

## Surface (wave 1)

| Folder | Surface | Role |
|---|---|---|
| `Core/` | `ChatClientConventions.Apply`, `AiPipelineOptions` | Uniform pipeline (function-invocation + OpenTelemetry) every broker terminates with |
| `Ollama/` | `AddOllamaChatClient(endpoint, model)`, `AddOllamaEmbeddingGenerator(...)` | Local models via the Ollama server |
| `Tokenizers/` | `AddTiktokenTokenCounter(model)`, `ITokenCounter` | tiktoken token counting for prompt-budget / cost |

## Quickstart

```csharp
builder.Services
    .AddOllamaChatClient("http://localhost:11434", "llama3.2")   // registers IChatClient (pipeline-wrapped)
    .AddOllamaEmbeddingGenerator(new Uri("http://localhost:11434"), "nomic-embed-text")
    .AddTiktokenTokenCounter("gpt-4o");

public sealed class Assistant(IChatClient chat, ITokenCounter tokens)
{
    public Task<ChatCompletion> AskAsync(string prompt, CancellationToken ct)
    {
        _ = tokens.Count(prompt);                       // budget check
        return chat.CompleteAsync(prompt, cancellationToken: ct);
    }
}
```

Every broker wraps its raw client with the SDK pipeline (`ChatClientConventions.Apply`) so tool-calling and OTel are on by default (toggle via `AiPipelineOptions`).

## Provider status

| Provider | Status |
|---|---|
| **Ollama** (local) | shipped (wave 1) |
| Tokenizers (tiktoken) | shipped (wave 1) |
| **Anthropic · OpenAI · Azure OpenAI** | pending — target M.E.AI `9.3.0+` (`GetResponseAsync` API); need a coordinated M.E.AI-stack version bump (+ newer OllamaSharp) that rewrites `Core` to the new API |
| Gemini · Bedrock · Llama · Semantic Kernel · vector stores · MCP | roadmap |

## Version note

The pinned `Microsoft.Extensions.AI 9.0.0-preview.9` uses the older `CompleteAsync`/`ChatCompletion` API — matched by OllamaSharp 4.x. Modern provider SDKs (Anthropic 5.1, OpenAI adapters) target `9.3.0+` (`GetResponseAsync`/`ChatResponse`). Aligning them = one coordinated stack bump; deferred so wave 1 ships stable.
