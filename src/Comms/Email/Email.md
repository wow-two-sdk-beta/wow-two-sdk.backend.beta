# Comms.Email

Transactional email seam: `IEmailSender.SendAsync(EmailMessage) → EmailSendResult` (result-typed,
no exceptions except cancellation). One provider registration per app:

| Provider | Registration | Notes |
|---|---|---|
| SMTP (MailKit) | `AddMailKitEmailSender(o => { o.Host = …; o.Username = …; })` | Any relay; mailpit/mailhog in dev |
| SendGrid | `AddSendGridEmailSender(o => o.ApiKey = …)` | v3 API |
| Amazon SES | `AddSesEmailSender(o => o.Region = "us-east-1")` | v2 simple send; no attachments yet |

```csharp
builder.Services
    .AddEmailDefaults(o => o.DefaultFrom = new EmailAddress("no-reply@example.com", "My App"))
    .AddMailKitEmailSender(o => { o.Host = "smtp.example.com"; o.Username = "u"; o.Password = "p"; });

var result = await _email.SendAsync(EmailMessage.Create("user@example.com", "Welcome!", textBody: "Hi."), ct);
```

- `From` resolution: message → `EmailOptions.DefaultFrom` → failure `no_from_address`.
- Future per the registry: Mailgun, Postmark, FluentEmail templating, sms/push siblings.
