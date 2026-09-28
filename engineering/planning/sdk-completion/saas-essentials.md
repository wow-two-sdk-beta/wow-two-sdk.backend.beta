# SaaS essentials — build track

*Last updated: 2026-09-28*

> Blocks every micro-SaaS launch reaches for in its first week and the SDK lacked: bot checks, free-tier quotas,
> direct uploads, branded email, and passwordless sign-in. None needs an owner decision; each is off until configured.

## Analysis — 2026-09-28

- No captcha check anywhere: public sign-up and contact forms had no bot gate.
- Rate limits bound bursts, but nothing counted a free plan's monthly allowance (3 conversions a day).
- Blob storage proxied every byte through the API; S3 and Azure SDKs already sign URLs.
- Emails were config texts; no layout, no Markdown body, no per-culture template.
- Sign-in needed a password, a phone, or a passkey; email-only sign-in was missing.

## Rulings

- Captcha is a broker per provider (Turnstile, hCaptcha, reCAPTCHA), keyed like SMS brokers; the token travels in a
  header or form field, never the JSON body, so the gate never consumes it.
- Captcha and quota gates are inert until their host section enables them, so local runs need no keys.
- Quotas count per subject and calendar period in UTC; a failed request refunds its units.
- A quota's limit may differ per plan; the plan comes from a claim, the subject from the user, API key or IP.
- Signed URLs are issued by the storage provider that holds the blob; local files get HMAC-signed SDK routes.
- Email templates are Markdown with `{placeholders}` in a shared responsive layout, with a plain-text twin.
- Passwordless sign-in reuses purpose tokens (link) and the OTP service (code); unknown addresses stay silent.

## Status

- [x] S1 — captcha: `ICaptchaBroker` (Turnstile, hCaptcha, reCAPTCHA v2/v3), `RequireCaptcha()`, `Web:Captcha`; 9 tests
- [x] S2 — usage quotas: `IQuotaService`, in-memory and Redis counters, `RequireQuota(name)`, usage read-out; 5 tests
- [x] S3 — signed blob URLs: read and write URLs for S3, Azure and local storage; local transfer endpoints; S3 now
  presigns SigV4 (AWSSDK fell back to SigV2); s3mock, Azurite and route tests
- [ ] S4 — email templates: Markdown templates per culture, layout, text twin, `SendTemplateAsync`
- [ ] S5 — passwordless email: magic link and email code sign-in in `SignInService` and the account API

---

## S1 — Captcha

- Acceptance: a missing, failed, stale-action or low-score token answers 400 before the handler; a disabled
  section passes every request; scripted provider responses cover each broker.

## S2 — Usage quotas

- Acceptance: the Nth+1 call in a period answers 429 with `Retry-After` at the period end; plans change the limit;
  a failed handler refunds; two hosts share counts through Redis.

## S3 — Signed blob URLs

- Acceptance: an S3 URL is presigned with the requested verb and expiry; an Azure SAS carries the permission; a
  local URL round-trips through the transfer endpoints and fails once expired or altered.

## S4 — Email templates

- Acceptance: a culture falls back to the default; placeholders fill; the HTML inlines its layout; the text twin has
  no markup; a missing template or value is refused.

## S5 — Passwordless email

- Acceptance: a link or code signs in once, expires, and fails after a stamp rotation; lockout and confirmation
  preconditions apply; sending to an unknown address reveals nothing.
