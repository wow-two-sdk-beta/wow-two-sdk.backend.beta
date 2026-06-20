# Web.Contracts

HTTP wire contracts shared by API hosts and their typed clients.

- **`ApiResponse` / `ApiResponse<T>`** — the result-pattern success envelope. Servers emit `ApiResponse<T>.Ok(dto)` (payload under `.data`); errors never travel here — they go out as RFC-7807 ProblemDetails. The `Failure` case is the client-side deserialization shape for non-2xx responses, so callers pattern-match instead of catching.

See also: `Web.Results` (failure category → HTTP status), `Web.ProblemDetails`.
