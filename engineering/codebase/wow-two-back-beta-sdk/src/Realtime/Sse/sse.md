# Realtime.Sse

*Server-Sent Events — one-way server→client streaming over a single long-lived HTTP response.*

The low-ceremony path for incremental results: LLM tokens, job progress, live feeds. Browser clients consume it with the native `EventSource` API (auto-reconnect + `Last-Event-ID` resumption built in).

## Surface

| Type | Role |
|---|---|
| `SseEvent` | One frame — `Data`, `EventName`, `Id`, `Retry`, `Comment` (+ `FromData`/`Named`/`KeepAlive` factories) |
| `SseWriter` | Encodes frames to a stream in `text/event-stream` format, flushing each; `WriteDataAsync<T>` JSON-serializes |
| `SseStreamOptions` | Heartbeat interval (default 15 s), event name, completion event, JSON options |
| `SseEndpointExtensions` | `HttpContext.WriteServerSentEventsAsync(...)` (enumerate + stream) · `StartServerSentEventStream()` (manual) |

## Two entry points

```csharp
// 1. Enumerate-and-stream — JSON per item, heartbeats, disconnect-safe:
await ctx.WriteServerSentEventsAsync(source, new SseStreamOptions { EventName = "token" }, ct);

// 2. Manual frame control (raw token strings, custom ids):
var sse = ctx.StartServerSentEventStream();
await foreach (var tok in tokens) await sse.WriteAsync(SseEvent.FromData(tok), ct);
```

## Notes

- Multi-line data is split into repeated `data:` lines per spec; `\r`/`\n` in event/id/comment are stripped.
- Heartbeats (comment frames) keep proxies from dropping an idle stream while a model is still thinking.
- Disconnect (client close / `RequestAborted`) ends the stream quietly — no error response.
- Set `CompletionEventName` (e.g. `done`) so clients can tell graceful end from a drop.
