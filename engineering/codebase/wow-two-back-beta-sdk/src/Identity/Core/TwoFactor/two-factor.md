# Identity.Core.TwoFactor

Second factors: the authenticator app (TOTP), single-use recovery codes, and delivered codes over any registered OTP
channel (SMS, WhatsApp, Telegram, email). Keys, codes and the preferred method live in `identity_user_tokens` under
ASP.NET Identity's provider name, so migrated authenticator keys keep working.

## Authenticator

```csharp
identity.AddEntityFrameworkStores<AppDbContext>().AddTwoFactor(o => o.Issuer = "Acme");

var key = await twoFactor.ResetAuthenticatorKeyAsync(user);        // show key / QR of GetAuthenticatorUriAsync
await twoFactor.EnableAsync(user, codeFromApp);                     // proves the app holds the key
var codes = await twoFactor.GenerateRecoveryCodesAsync(user);       // show once
```

## Delivered methods

A method is a name the user picks, bound to a channel, a broker of that channel and a code shape. The host
configuration alone can add, reroute or reshape one:

```jsonc
"Identity": {
  "TwoFactor": {
    "Issuer": "Acme",
    "AutoSendCode": true,
    "Methods": {
      "sms":      { "Channel": "sms", "Broker": "eskiz" },
      "whatsapp": { "Channel": "whatsapp", "Broker": "meta" },
      "telegram": { "Channel": "telegram-gateway" },
      "email":    { "Channel": "email", "Code": { "Kind": "Alphanumeric", "Length": 8, "Lifetime": "00:10:00" } }
    }
  }
}
```

```csharp
builder.Services.AddSmsBrokers(builder.Configuration).AddSmsOtpDelivery().AddEmailOtpDelivery();   // channels

var available = await methods.GetAvailableMethodsAsync(user);        // ["authenticator", "sms"] — confirmed addresses only
await methods.SendCodeAsync(user, "sms");                            // worded per culture, sent through eskiz
await methods.EnableAsync(user, "sms", code);                        // enables two-factor, makes sms preferred
```

- Addresses: phone for SMS, WhatsApp and Telegram Gateway; email for email; the `Telegram` login for the bot channel.
  Override with `Address` and `LoginProvider`. A method without a registered channel or confirmed address is not offered.
- Codes live in `IOtpService` under `identity.two-factor.{method}` with the method's own spec; wording comes from
  `IOtpMessageFormatter` with the purpose `two-factor` (template keys such as `two-factor.sms`).
- `SignInService`: a `RequiresTwoFactor` result names `TwoFactorMethod`; `SendTwoFactorCodeAsync(userId, ticket)` sends it
  and `TwoFactorSignInAsync(userId, ticket, method, code)` completes the sign-in.

## Notes

- Recovery codes are stored as SHA-256 digests; redemption ignores case and the dash.
- Enabling, disabling and resetting the key rotate the security stamp; disabling forgets the key, codes and preference.
- Two-factor sign-in also needs `.AddUserTokens(...)` for the ticket between the two steps.
