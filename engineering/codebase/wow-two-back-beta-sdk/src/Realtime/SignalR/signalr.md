# Realtime.SignalR

*SignalR with conventional defaults + built-in per-node presence tracking.*

## Surface

| Type | Role |
|---|---|
| `AddConventionalSignalR(configure?)` | SignalR + bounded keep-alive/timeout/size + SDK JSON protocol; registers the tracker. Returns `ISignalRServerBuilder` |
| `SignalRConventionOptions` | Keep-alive (15 s), client timeout (30 s), handshake (15 s), max receive (64 KB), detailed errors, JSON options |
| `SdkHub` / `SdkHub<TClient>` | Hub base adding `UserId` + `ConnectionId` conveniences |
| `PresenceHub` | Auto-tracks connect/disconnect via `IUserConnectionTracker` |
| `IUserConnectionTracker` / `InMemoryUserConnectionTracker` | user ↔ connection-ids map; `IsOnline` / `GetConnections` / `OnlineUsers` |
| `AddRedisBackplane(connStr, configure?)` | StackExchange.Redis scale-out backplane (`ISignalRServerBuilder` extension) |

## Quickstart

```csharp
builder.Services
    .AddConventionalSignalR(o => o.EnableDetailedErrors = env.IsDevelopment())
    .AddRedisBackplane(cfg.GetConnectionString("redis")!);   // multi-node only

app.MapHub<ChatHub>("/hubs/chat");

public sealed class ChatHub(IUserConnectionTracker tracker) : PresenceHub(tracker)
{
    public Task Send(string text) => Clients.All.SendAsync("received", UserId, text);
}
```

## Notes

- JSON protocol uses a **mutable copy** of the SDK wire preset (camelCase) — SignalR attaches its own converters and would throw on the frozen shared instance.
- `IUserConnectionTracker` is **per-node** (in-memory). The backplane fans out *messages* across nodes but not presence — add a distributed store for cluster-wide "who's online".
- The receive-size cap (64 KB) blunts oversized-frame abuse; raise `MaximumReceiveMessageSize` for large payloads or set `null` to remove it.
