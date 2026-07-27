# Realtime

*Server-push over three transports: SignalR (bidirectional RPC + presence), SSE (one-way streaming — the LLM-token sweet spot), and raw WebSockets.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Realtime`. All three transports ship in the ASP.NET Core shared framework; only the SignalR **Redis backplane** pulls one add-on package.

## Pick a transport

| Need | Use | Why |
|---|---|---|
| Bidirectional RPC, groups, presence, reconnect | **SignalR** (`SignalR/`) | Full-duplex, client proxies, transport fallback |
| Server → browser stream (LLM tokens, progress, feeds) | **SSE** (`Sse/`) | Plain HTTP, auto-reconnect via `EventSource`, no handshake |
| Custom binary/text protocol, full control | **WebSockets** (`WebSockets/`) | Raw socket, no framing opinions |

## SignalR — quickstart

```csharp
builder.Services
    .AddConventionalSignalR(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment())
    .AddRedisBackplane(builder.Configuration.GetConnectionString("redis")!); // omit for single-node

app.MapHub<ChatHub>("/hubs/chat");

// Presence for free — derive from PresenceHub:
public sealed class ChatHub(IUserConnectionTracker tracker) : PresenceHub(tracker) { }
// Query presence anywhere: IUserConnectionTracker.IsOnline(userId) / GetConnections(userId)
```

`IUserConnectionTracker` is in-memory / **per-node** — pair the backplane with a distributed store for cluster-wide presence.

## SSE — quickstart (LLM streaming)

```csharp
app.MapGet("/chat/stream", (HttpContext ctx, string prompt, IChatClient llm, CancellationToken ct) =>
    ctx.WriteServerSentEventsAsync(
        llm.CompleteStreamingAsync(prompt, ct).Select(u => u.Text),   // IAsyncEnumerable<string>
        new SseStreamOptions { EventName = "token", CompletionEventName = "done" },
        ct));
```

Sets the `text/event-stream` headers, disables buffering, flushes per item, sends a 15 s heartbeat while idle, and stops cleanly on client disconnect. For raw control use `ctx.StartServerSentEventStream()` → an `SseWriter`, or stream pre-built `SseEvent` frames.

## WebSockets — quickstart

```csharp
app.UseConventionalWebSockets();                          // keep-alive + origin allowlist

app.MapGet("/ws/echo", (HttpContext ctx) => ctx.AcceptWebSocketAsync(new EchoHandler()));

sealed class EchoHandler : IWebSocketConnectionHandler
{
    public async Task HandleAsync(WebSocket s, HttpContext ctx, CancellationToken ct)
    {
        await foreach (var msg in s.ReceiveTextMessagesAsync(cancellationToken: ct))
            await s.SendTextAsync($"echo: {msg}", ct);
    }
}
```

`AcceptWebSocketAsync` runs the accept + closing handshake and swallows client-disconnect cancellation; `ReceiveTextMessagesAsync` reassembles fragmented UTF-8 messages.

## See also

- `SignalR/signalr.md` · `Sse/sse.md` · `WebSockets/websockets.md`
- Caching vector (Redis) — backplane transport · Ai vector — the primary SSE payload source
