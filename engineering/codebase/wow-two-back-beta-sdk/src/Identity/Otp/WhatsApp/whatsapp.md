# Identity.Otp.WhatsApp

`WhatsAppOtpDeliveryHandler` — the `whatsapp` channel over an `IWhatsAppBroker` (`Comms/WhatsApp`).

```csharp
services.AddWhatsAppBrokers(configuration)                   // Comms:WhatsApp:{Meta|Twilio}
        .AddWhatsAppOtpDelivery(o =>                         // or Identity:Otp:WhatsApp
        {
            o.TemplateName = "auth_code";                    // an approved authentication template
            o.TemplateLanguages["ru"] = "ru";
        });
```

- With `TemplateName`, the code fills `{{1}}` and the copy-code button (`CopyCodeButton`); the language follows the
  envelope's culture through `TemplateLanguages`, else `TemplateLanguage` (`en_US`).
- Without it, the envelope's `Text` (or the formatter's) goes as free text, which reaches only recipients who wrote
  to the business within 24 hours.
