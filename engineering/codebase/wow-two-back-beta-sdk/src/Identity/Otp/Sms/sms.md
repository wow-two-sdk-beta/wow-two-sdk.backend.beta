# Identity.Otp.Sms

`SmsOtpDeliveryHandler` — the `sms` channel: sends codes through the SMS broker the envelope or `Broker` names, else
the default broker (`Comms:Sms:DefaultBroker`, then the first registered).

```csharp
services.AddSmsBrokers(configuration)                        // Comms:Sms:{Twilio|Vonage|Eskiz}
        .AddSmsOtpDelivery(o => o.Broker = "eskiz");         // or Identity:Otp:Sms:Broker
```

- The envelope's `Text` is sent as is; without it the fallback `MessageTemplate` applies: `{0}` scope display name,
  `{1}` code, `{2}` lifetime minutes.
- The envelope's `DeliveryAddress` is the E.164 number; the broker's failure reason passes through unchanged, and an
  unknown broker name fails with `sms_broker_not_registered`.
