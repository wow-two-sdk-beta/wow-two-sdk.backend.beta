# HTTP ProblemDetails — standard

*Last updated: 2026-10-02*

> Opt-in RFC 9457 error bodies across the writable HTTP boundary.

## Contract

- MUST pair `AddHttpProblemDetails()` with `UseHttpProblemDetails()` outside product middleware.
- MUST retain successful response contracts.
- MUST emit `application/problem+json` for writable HTTP `4xx` and `5xx` errors.
- MUST handle empty statuses, MVC error results and thrown exceptions.
- MUST retain validation field errors and mapped application error metadata.
- MUST normalize MVC errors whose status comes from the result or the HTTP response.
- MUST customize each problem once, retaining supplied exception and endpoint metadata.
- MUST retain authentication, retry and method headers.
- MUST fall back to JSON when `Accept` excludes JSON.
- MUST include matching `status`, `type`, `title`, `instance`, `traceId` and `requestId`.
- MUST mark failures `Cache-Control: no-store`.
- MUST keep unexpected exception detail and stack traces server-side.
- MUST NOT replace a started response or emit a body for `HEAD`.
- MUST NOT fabricate a deliverable response after client disconnection.

## Serialization

The opt-in writer serializes JSON directly. MVC formatter negotiation with
`ReturnHttpNotAcceptable=true` can otherwise omit the error body, including for
`Accept: application/json`; the HTTP boundary bypasses that negotiation for errors.

## Host boundary

- Product middleware MUST return ProblemDetails for intentional error payloads.
- A middleware-produced nonempty error body is not buffered or rewritten.
- Host/proxy failures before this application pipeline require their own equivalent handling.
- `AddApiDefaults()` does not enable this boundary; full normalization remains an explicit host opt-in.

The opt-in registration places the boundary before framework startup middleware.
Host filtering suppresses its HTML failure message so rejected hosts also receive
ProblemDetails. Repeated `UseHttpProblemDetails()` calls do not duplicate the pipeline.
