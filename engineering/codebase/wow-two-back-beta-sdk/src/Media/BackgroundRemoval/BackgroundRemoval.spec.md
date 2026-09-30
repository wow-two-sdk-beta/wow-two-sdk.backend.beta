# Background removal API

Register `AddBackgroundRemoval(options => ...)` with explicit BaseUrl and ApiKey. Only HTTPS or literal loopback HTTP origins are accepted. Registration disables redirects and cookies.
`GetStatusAsync` returns actual ready/model state. `RemoveAsync` accepts bytes and returns PNG bytes, upright dimensions and model. No model downloading, files, URLs, persistence or publishing occur.

Worker contract: authenticated `GET /health/ready` returns `{ready:true,model:"u2net"}`; authenticated `POST /v1/remove-background` takes raw image bytes and returns PNG plus `X-Image-Model`.
Both requests carry the host-only `X-Image-Worker-Key`.

Default limits: 20MiB encoded, 20MP decoded, 8192 per side, 90 seconds processing, 3 seconds readiness and 4KiB readiness JSON. Inputs decode fully as single-frame PNG, JPEG or WebP; output must preserve upright dimensions. `BackgroundRemovalException.Code` distinguishes invalid input, unavailable worker and invalid output; errors contain no provider content.

Hosts authenticate callers, preserve originals and track transformation provenance. Publication approval remains separate.
