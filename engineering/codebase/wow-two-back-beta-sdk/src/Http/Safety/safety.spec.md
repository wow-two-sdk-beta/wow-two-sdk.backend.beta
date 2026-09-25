# Outbound HTTP safety

*Last updated: 2026-09-26*

## Contract

`AddSafeOutboundHttp` is opt-in on a named or typed `IHttpClientBuilder`. It validates
absolute HTTP(S) destinations, requires HTTPS by default, rejects URI credentials and
optionally restricts exact normalized DNS hosts. Address validation occurs at connection
time and the socket dials only the validated addresses. Redirects, system proxies and
shared cookies are disabled. HTTP/3 and version-upgrade requests are rejected.

The caller must consume redirect responses explicitly, reapply its business policy, and
send any approved destination through the same guarded client. The extension owns the primary
handler; replacing or reconfiguring that handler can invalidate its guarantees.
Private network access requires explicit registration configuration.
This transport does not enforce response-size limits, business URL allowlists or request replay policy.

## Usage

```csharp
services.AddHttpClient("remote-document")
    .AddSafeOutboundHttp(options => options.AllowedHosts.Add("example.com"))
    .AddSdkResilience();
```
