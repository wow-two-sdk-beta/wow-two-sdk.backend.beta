# Comms.WhatsApp

WhatsApp Business seam: `IWhatsAppBroker.SendAsync(WhatsAppMessage) → WhatsAppSendResult` (result-typed, no
exceptions except cancellation). Brokers register by name; the host configuration decides which exist.

| Provider | Registration | Section · notes |
|---|---|---|
| Meta Cloud API | `AddMetaWhatsAppBroker()` → `meta` | `Comms:WhatsApp:Meta` · `AccessToken`, `PhoneNumberId`, `ApiVersion` |
| Twilio | `AddTwilioWhatsAppBroker()` → `twilio` | `Comms:WhatsApp:Twilio` · template = content SID, numbered variables |

```csharp
builder.Services.AddWhatsAppBrokers(builder.Configuration);          // registers each provider whose section exists

var meta = factory.Create("meta");                                   // IWhatsAppBrokerFactory; Create() = DefaultBroker
await meta!.SendAsync(new WhatsAppMessage
{
    To = "+998901234567",
    Template = new WhatsAppTemplate { Name = "auth_code", LanguageCode = "ru", BodyParameters = ["123456"], ButtonParameter = "123456" },
}, ct);
```

- Business-initiated messages need an approved template; free `Text` reaches only a recipient inside the 24-hour window.
- `Comms:WhatsApp:DefaultBroker` picks the default; otherwise the first registered broker is.
- Each registration returns the `IHttpClientBuilder`, so hosts add resilience or a test handler to it.
- OTP codes by WhatsApp: `AddWhatsAppOtpDelivery()` in `Identity/Otp/WhatsApp`.
