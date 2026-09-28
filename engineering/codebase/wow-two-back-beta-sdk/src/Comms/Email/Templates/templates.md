# Comms.Email.Templates

Branded transactional email from Markdown: one template per name and culture, `{placeholders}` with ICU plurals, a
responsive layout with inlined styles, and a plain-text twin. Copy lives in configuration, so edits need no rebuild.

## Quick start

```csharp
builder.Services.AddEmailTemplates();                 // Comms:Email:Templates; pair with any IEmailBroker

await emails.SendAsync(new EmailAddress { Address = user.Email }, "welcome",
    new Dictionary<string, object?> { ["Name"] = user.Name, ["Link"] = confirmUrl }, CultureInfo.GetCultureInfo("uz"));
```

```jsonc
"Comms": { "Email": { "Templates": {
  "Brand": { "Name": "Tools", "LogoUrl": "https://cdn.example/logo.png", "AccentColor": "#0F766E", "FooterText": "Tools LLC" },
  "Templates": { "welcome": {
    "en": { "Subject": "Welcome, {Name}", "Body": "# Hi {Name}\n\n[Confirm your email]({Link} \"button\")" },
    "uz": { "Subject": "Xush kelibsiz, {Name}", "Body": "…" }
  } }
} } }
```

## Notes

- Culture lookup: `uz-Latn-UZ` → `uz-Latn` → `uz` → `DefaultCulture` (`en`).
- A paragraph holding only a link titled `"button"` renders as a button; other links take the accent color.
- String values are Markdown-escaped before placing, so a user's name never becomes a link or markup.
- Raw HTML in a template shows as text; the layout is table-based and single-column for mail clients.
- The text twin keeps headings, lists and tables, and writes each link as `label (url)`.
- A missing template or placeholder value raises `EmailTemplateRejectedException` naming what is missing.
