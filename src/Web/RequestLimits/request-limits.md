# Web.RequestLimits

Bounds otherwise-unbounded request input at the Kestrel layer: body size, total header size, request-line size, and
header-arrival timeout. Closes the decompression-bomb / slow-loris / oversized-header surface.

## Wire-up

```csharp
services.AddRequestLimits();                 // defaults: 30 MB body · 32 KB headers · 8 KB request line · 30s header timeout
services.AddRequestLimits(o =>
{
    o.MaxRequestBodySizeBytes = 5L * 1024 * 1024;   // tighten for a JSON-only API
    o.RequestHeadersTimeout   = TimeSpan.FromSeconds(15);
});
```

Called automatically by `AddProxyAwareHosting()` (and therefore by `AddApiDefaults()`) so the shipped
`UseRequestDecompression` never runs unbounded — `MaxRequestBodySize` caps the **compressed** input the decompressor reads.

## Notes

- Kestrel-only (configures `KestrelServerOptions.Limits`); no effect under IIS/HTTP.sys in-process.
- `MaxRequestBodySize` bounds the compressed request; a per-endpoint hard cap on **decompressed** output (for extreme
  high-ratio bombs) is a follow-up — read bodies with a bounded reader where that matters.

## See also

- `../Hosting/hosting.md` (proxy-aware hosting wires this in) · `../../Meta/` boot floor.
