# Comms.Sms

Transactional SMS seam: `ISmsBroker.SendAsync(SmsMessage) → SmsSendResult` (result-typed, no exceptions except
cancellation). Brokers register by name; every broker speaks plain HTTP, no vendor SDK.

| Provider | Registration | Section · notes |
|---|---|---|
| Twilio | `AddTwilioSmsBroker()` → `twilio` | `Comms:Sms:Twilio` · `MessagingServiceSid` replaces `From` |
| Vonage | `AddVonageSmsBroker()` → `vonage` | `Comms:Sms:Vonage` · unicode text; every part must report `0` |
| Eskiz (UZ) | `AddEskizSmsBroker()` → `eskiz` | `Comms:Sms:Eskiz` · bearer token cached, re-sign-in once on 401 |

```csharp
builder.Services.AddSmsBrokers(builder.Configuration);        // each provider whose Comms:Sms:{Provider} section exists
// or in code: .AddSmsDefaults(o => o.DefaultFrom = "Acme").AddEskizSmsBroker(o => { o.Email = …; o.Password = …; });

var result = await sms.SendAsync(new SmsMessage { To = "+998901234567", Body = "Your order shipped." }, ct);
var eskiz  = factory.Create("eskiz");                          // ISmsBrokerFactory; Create() = the default broker
```

- Options: code first, then the host section, which has the last word (`Comms:Sms:DefaultFrom`, `…:DefaultBroker`).
- Default broker: `Comms:Sms:DefaultBroker`, else the first registered; `ISmsBroker` resolves the first registered.
- Recipients are E.164; digit-only providers (Vonage, Eskiz) receive the number without `+`.
- `From` resolution: message → `SmsOptions.DefaultFrom` → provider default (Eskiz `4546`); Twilio and Vonage fail with `no_from_address`.
- Each registration returns the `IHttpClientBuilder`, so hosts add resilience or a test handler to it.
- Eskiz sends only moderated templates; OTP wording must match an approved template.
- OTP codes by SMS: `AddSmsOtpDelivery()` in `Identity/Otp/Sms`.
