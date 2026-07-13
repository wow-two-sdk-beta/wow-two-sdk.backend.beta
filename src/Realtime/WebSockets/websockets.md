# Realtime.WebSockets

*Raw WebSocket conventions — full-duplex socket with no framing opinions.*

Reach for this only when SignalR's RPC model or SSE's one-way stream don't fit (custom sub-protocol, binary framing). Otherwise prefer SignalR.

## Surface

| Type | Role |
|---|---|
| `UseConventionalWebSockets(configure?)` | WebSocket middleware with keep-alive (15 s) + origin allowlist |
| `WebSocketConventionOptions` | `KeepAliveInterval`, `AllowedOrigins` |
| `IWebSocketConnectionHandler` | `HandleAsync(socket, httpContext, ct)` — drives one connection |
| `HttpContext.AcceptWebSocketAsync(handler)` | Accept + closing handshake; `400` on non-WS requests; disconnect-safe |
| `WebSocket.ReceiveTextMessagesAsync(...)` | `IAsyncEnumerable<string>` reassembling fragmented UTF-8 messages |
| `WebSocket.SendTextAsync(text, ct)` | Single UTF-8 text frame |

## Quickstart

```csharp
app.UseConventionalWebSockets(o => o.AllowedOrigins.Add("https://app.example.com"));

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

## Notes

- `AcceptWebSocketAsync` owns the socket lifetime (`using`) and performs the closing handshake — the handler just returns when done.
- `ReceiveTextMessagesAsync` ignores binary frames and yields on `EndOfMessage`; peer close or cancellation ends the sequence.
- Empty `AllowedOrigins` accepts any origin — populate it to prevent cross-site socket hijacking.
