# Web.Captcha

Bot checks for public forms: the widget's token is verified with the provider before the handler runs. One
siteverify broker covers Cloudflare Turnstile, hCaptcha and Google reCAPTCHA v2/v3.

## Quick start

```csharp
builder.Services.AddCaptcha();                                  // off until Web:Captcha:Enabled
app.MapPost("/account/register", …).RequireCaptcha("signup");  // action optional
```

```jsonc
"Web": { "Captcha": {
  "Enabled": true, "Provider": "turnstile", "Secret": "0x4AAA…",
  "ExpectedHostnames": [ "app.example" ], "MinimumScore": 0.5
} }
```

- Clients send the token in `X-Captcha-Token`, or as the widget's form field (`cf-turnstile-response`,
  `h-captcha-response`, `g-recaptcha-response`) on form posts. JSON bodies are never read by the gate.
- A missing, rejected, wrong-host, wrong-action or low-score token answers `400` (`messageKey` `CaptchaInvalid`).
- A provider outage answers `503`; `FailOpen: true` lets requests through instead.
- `ICaptchaValidator.ValidateAsync(context, action)` checks outside minimal APIs, such as in a controller.
- Turnstile's published test secrets (`1x000…AA` passes, `2x000…AA` fails) suit staging.
