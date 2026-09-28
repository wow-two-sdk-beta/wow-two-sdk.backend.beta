# Web.Idempotency

HTTP `Idempotency-Key` handling (IETF draft), switched on by configuration alone. Off by default;
`AddApiDefaults` / `UseApiDefaults` already register it inert, after authentication.

```jsonc
"HttpIdempotency": { "Enabled": true, "Ttl": "1.00:00:00", "MaxBodyBytes": 1048576 }
```

```http
POST /orders
Idempotency-Key: 7f9c1c1e-…
```

- A POST or PATCH carrying the header executes once per key, method, path and caller; retries replay the stored
  status, content type and body with `Idempotent-Replayed: true`.
- The same key with another body answers `422`; a retry racing the unfinished original answers `409`.
- Server errors, exceptions and oversized or streamed responses are not stored, so their retries execute again.
- Storage is `IIdempotencyRepository`: in-memory by default; `AddSqlIdempotencyRepository()` shares keys across hosts.
- Requests without the header are untouched; the mediator's `IIdempotent` deduplication is a separate, in-process seam.
