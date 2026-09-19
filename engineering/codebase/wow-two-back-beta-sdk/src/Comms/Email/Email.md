# Comms.Email

Transactional email seam: `IEmailBroker.SendAsync(EmailMessage) → EmailSendResult` (result-typed,
no exceptions except cancellation). One provider registration per app:

| Provider | Registration | Notes |
|---|---|---|
| SMTP (MailKit) | `AddMailKitEmailBroker(o => { o.Host = …; o.Username = …; })` | Any relay; mailpit/mailhog in dev |
| SendGrid | `AddSendGridEmailBroker(o => o.ApiKey = …)` | v3 API |
| Amazon SES | `AddSesEmailBroker(o => o.Region = "us-east-1")` | v2 simple send; no attachments yet |

```csharp
builder.Services
    .AddEmailDefaults(o => o.DefaultFrom = new EmailAddress("no-reply@example.com", "My App"))
    .AddMailKitEmailBroker(o => { o.Host = "smtp.example.com"; o.Username = "u"; o.Password = "p"; });

var result = await _email.SendAsync(EmailMessage.Create("user@example.com", "Welcome!", textBody: "Hi."), ct);
```

- `From` resolution: message → `EmailOptions.DefaultFrom` → failure `no_from_address`.
- Future per the registry: Mailgun, Postmark, FluentEmail templating, sms/push siblings.
