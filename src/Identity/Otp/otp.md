# Identity.Otp

One-time passwords keyed by `(subject, scope)` — subject is any string you key auth on (phone,
email, username); the SDK has no user model. Orthogonal seams, all swappable via DI:

| Seam | Default |
|---|---|
| `IOtpService` | `OtpService` — rate limit → generate → store; verify with fixed-time compare, attempt counting, consume-on-success |
| `IOtpStore` | `MemoryOtpStore` (singleton; dev / single instance — use a shared store for multi-instance) |
| `IOtpCodeGenerator` | `NumericOtpCodeGenerator` — crypto-random digits, `CodeLength` long |
| `IOtpDeliveryHandler` | none — pick a channel package (`Otp/Telegram`) or implement your own |

```csharp
builder.Services.AddOtpService(o =>
{
    o.CodeLength      = 6;
    o.CodeLifetime    = TimeSpan.FromMinutes(5);
    o.RateLimitWindow = TimeSpan.FromSeconds(5);
    o.MaxAttempts     = 5;
});

// request-otp endpoint: consumer looks up its user, then
var creation = await _otp.CreateAsync(body.Phone, body.Service, ct);          // returns the code
var delivery = await _delivery.SendAsync(new OtpDeliveryEnvelope(user.TelegramChatId, creation.Code!, body.Service), ct);

// verify-otp endpoint
var verification = await _otp.VerifyAsync(body.Phone, body.Code, body.Service, ct);
```

- **Creation returns the code; the caller delivers.** Delivery address ≠ subject (phone in, chat-id out) — only the consumer knows the mapping.
- Failure reasons: `RateLimited` · `Expired` · `InvalidCode` · `MaxAttemptsReached`.
- Future companions: `Otp.Postgres` / `Otp.Redis` stores, `Otp.Sms` / `Otp.Email` channels.

Composes with `Identity/Policies` (role gating) + `Identity/Jwt/Issuance` (token on success) into a
full passwordless login — the consumer's auth endpoint is ~30 lines of glue.
