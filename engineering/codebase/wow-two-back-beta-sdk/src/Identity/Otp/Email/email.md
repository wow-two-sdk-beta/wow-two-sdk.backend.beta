# Identity.Otp.Email

`EmailOtpDeliveryHandler` — the `email` channel over the registered `IEmailBroker`.

```csharp
services.AddMailKitEmailBroker(…).AddEmailOtpDelivery(o => o.From = "security@acme.test");   // or Identity:Otp:Email
```

- Subject and text come from the envelope, else from `IOtpMessageFormatter` (`{purpose}.email.subject`, …).
- An address without `@` fails with `invalid_email_address` before the broker is called.
