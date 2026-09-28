# Web.ConditionalRequests

Entity tags and `304 Not Modified` for GET responses, switched on by configuration alone. Off by default;
`AddApiDefaults` / `UseApiDefaults` already register it inert.

## Enable

```jsonc
"ConditionalRequests": { "Enabled": true, "MaxBufferBytes": 1048576 }
```

- Successful GET bodies get a weak `ETag` (SHA-256 of the body); a matching `If-None-Match` answers `304` with no body.
- An endpoint's own tag wins: `http.Response.SetEntityTag(row.Xmin)` — revalidation then compares against it.
- SSE, NDJSON and bodies over `MaxBufferBytes` stream through untagged; the section reloads live.

## Optimistic writes

```csharp
if (http.Request.FailsIfMatch(order.Xmin))
    return Results.StatusCode(StatusCodes.Status412PreconditionFailed);
```

- `FailsIfMatch` refuses only a present `If-Match` that names neither the current version nor `*`.
