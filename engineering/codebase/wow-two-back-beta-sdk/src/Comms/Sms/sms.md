# Comms.Sms

Transactional SMS seam: `ISmsBroker.SendAsync(SmsMessage) → SmsSendResult` (result-typed, no exceptions except
cancellation). One provider registration per app; every broker speaks plain HTTP, no vendor SDK.

| Provider | Registration | Notes |
|---|---|---|
| Twilio | `AddTwilioSmsBroker(o => { o.AccountSid = …; o.AuthToken = …; })` | `MessagingServiceSid` replaces `From` |
| Vonage | `AddVonageSmsBroker(o => { o.ApiKey = …; o.ApiSecret = …; })` | unicode text; every part must report `0` |
| Eskiz (UZ) | `AddEskizSmsBroker(o => { o.Email = …; o.Password = …; })` | bearer token cached, re-sign-in once on 401 |

```csharp
builder.Services
    .AddSmsDefaults(o => o.DefaultFrom = "Acme")
    .AddEskizSmsBroker(o => { o.Email = cfg["Eskiz:Email"]!; o.Password = cfg["Eskiz:Password"]!; });

var result = await sms.SendAsync(new SmsMessage { To = "+998901234567", Body = "Your order shipped." }, ct);
```

- Recipients are E.164; digit-only providers (Vonage, Eskiz) receive the number without `+`.
- `From` resolution: message → `SmsOptions.DefaultFrom` → provider default (Eskiz `4546`); Twilio and Vonage fail with `no_from_address`.
- Each registration returns the `IHttpClientBuilder`, so hosts add resilience or a test handler to it.
- Eskiz sends only moderated templates; OTP wording must match an approved template.
- OTP codes by SMS: `AddSmsOtpDelivery()` in `Identity/Otp/Sms`.
