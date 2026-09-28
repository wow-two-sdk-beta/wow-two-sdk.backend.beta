# Identity.Otp.Sms

`SmsOtpDeliveryHandler` — an additive `IOtpDeliveryHandler` that sends codes through the registered `ISmsBroker`.

```csharp
services.AddOtpService()
        .AddSmsOtpDelivery(o => o.MessageTemplate = "{1} - Acme tasdiqlash kodi. {2} daqiqa amal qiladi.")
        .AddEskizSmsBroker(o => { o.Email = …; o.Password = …; });
```

- Template placeholders: `{0}` scope display name, `{1}` code, `{2}` lifetime minutes (from `OtpOptions.CodeLifetime`).
- The envelope's `DeliveryAddress` is the E.164 number; the broker's failure reason passes through unchanged.
