# Identity.Otp

One-time codes keyed by `(subject, scope)` — the subject is any string auth keys on (phone, email, user id). Every
seam swaps through DI; every option set comes from code, then the host section, which has the last word.

| Seam | Default |
|---|---|
| `IOtpService` | `OtpService` — rate limit → generate → store; verify in fixed time, count attempts, consume on success |
| `IOtpRepository` | `MemoryOtpRepository` (single instance; register a shared store before `AddOtpService` for more) |
| `IOtpCodeGenerator` | `OtpCodeGenerator` — crypto-random codes to an `OtpCodeSpec` (numeric, alphanumeric, letters) |
| `IOtpMessageFormatter` | `OtpMessageFormatter` — per-culture ICU templates, built-in `en` · `ru` · `uz` |
| `IOtpDeliveryHandlerFactory` | resolves the handler registered for a channel name |

## Channels

| Channel | Registration | Address · notes |
|---|---|---|
| `sms` | `AddSmsOtpDelivery()` + an SMS broker | E.164 · envelope or `Identity:Otp:Sms:Broker` names the broker (`eskiz`, `twilio`, …) |
| `whatsapp` | `AddWhatsAppOtpDelivery()` + a WhatsApp broker | E.164 · approved authentication template, language per culture |
| `telegram` | `AddTelegramOtpDelivery()` + `ITelegramBotClient` | chat id of a user who started the bot |
| `telegram-gateway` | `AddTelegramGatewayOtpDelivery()` | E.164 · Telegram Gateway API; numeric codes of 4–8 digits |
| `email` | `AddEmailOtpDelivery()` + an email broker | email address · subject and text from the formatter |

Each registers keyed by its channel name and stays enumerable as `IOtpDeliveryHandler`.

## Configure

```jsonc
"Identity": {
  "Otp": {
    "CodeLength": 6, "CodeKind": "Numeric", "CodeLifetime": "00:05:00", "RateLimitWindow": "00:00:30", "MaxAttempts": 5,
    "Messages": {
      "AppName": "Acme",
      "Templates": { "ru": { "two-factor.sms": "Код {app}: {code}", "subject": "Ваш код {app}" } }
    },
    "Sms": { "Broker": "eskiz" },
    "WhatsApp": { "Broker": "meta", "TemplateName": "auth_code", "TemplateLanguages": { "ru": "ru", "uz": "uz" } },
    "TelegramGateway": { "AccessToken": "…" },
    "Email": { "From": "security@acme.test", "FromName": "Acme" }
  }
}
```

- Template keys, most specific first: `{purpose}.{channel}` → `{purpose}` → `{channel}` → `text` (subjects add `.subject`).
- Placeholders: `{code}` · `{minutes}` · `{app}` · `{purpose}`; plurals as `{minutes, plural, one {# minute} other {# minutes}}`.
- Culture: the requested culture and its parents, then `DefaultCulture`, then `en`; configured templates beat built-in ones.

```csharp
var created = await otp.CreateAsync(user.Id, "phone-confirmation", new OtpCodeSpec { Kind = OtpCodeKind.Alphanumeric, Length = 8 }, ct);
var worded  = formatter.Format("phone-confirmation", OtpChannelNameConstants.Sms, created.Code!, TimeSpan.FromMinutes(5));
await handlers.Create(OtpChannelNameConstants.Sms)!.SendAsync(new OtpDeliveryEnvelopeModel
{
    DeliveryAddress = user.PhoneNumber!, Code = created.Code!, Scope = "phone-confirmation", Text = worded.Text,
}, ct);
var verified = await otp.VerifyAsync(user.Id, typedCode, "phone-confirmation", ct);   // ignores case, spaces, dashes
```

- Creation returns the code; the caller delivers, because only it knows the address behind the subject.
- An envelope without `Text` falls back to the handler's own wording (SMS/Telegram `MessageTemplate`, else the formatter).
- Failure reasons: `RateLimited` · `Expired` · `InvalidCode` · `MaxAttemptsReached`.
- Two-factor sign-in over these channels: `Identity/Core/TwoFactor`.
