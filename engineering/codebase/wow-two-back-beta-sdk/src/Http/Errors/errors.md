# Http.Errors

Outbound HTTP failures as application errors — the `HttpClientError` piece of the errors design.

- `HttpExceptionMappingRule` (registered by `AddApiDefaults` / `AddHttpExceptionMapping`) classifies exceptions:
  unreachable, 5xx, 429 and credential-rejecting (401/403) dependencies → `ExternalUnavailable` (503);
  408/504 and client timeouts → `OperationTimeout`; outbound-safety blocks → `Forbidden`; other 4xx stay unmapped.
- A dependency's 401 never becomes the caller's 401, which would sign a user out for the server's own bad credential.

```csharp
using var response = await http.GetAsync($"quotes/{id}", ct);
await response.EnsureSuccessAsync(ct);          // throws AppException with upstreamStatus / upstreamDetail / retryAfter
// or: if (await response.ToAppErrorAsync(ct) is { } error) return Result<Quote>.Fail(error);
```

- `ToAppErrorAsync` also maps 404 → `NotFound` and 409 → `Conflict` for proxy-style reads; other 4xx → `Unexpected`.
