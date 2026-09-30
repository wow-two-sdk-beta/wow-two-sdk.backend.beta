# Background removal contract

- Hosts MUST authenticate and authorize callers before invoking this optional adapter.
- Inputs MUST decode completely as single-frame PNG, JPEG or WebP within explicit byte, pixel and dimension limits.
- Outputs MUST decode completely as PNG and preserve EXIF-upright dimensions within the same bounds.
- The adapter MUST reject redirects and never follow caller-controlled URLs or paths.
- Worker configuration MUST be explicit; unconfigured or unreachable workers report unavailable.
- HTTP is permitted only for literal loopback addresses. Remote workers require HTTPS.
- Readiness JSON and processing time MUST be bounded; cancellation MUST propagate.
- Hosts own model provisioning, provenance, asset persistence and publication approval.
- This adapter stores nothing, downloads no models and never logs image bytes or credentials.
