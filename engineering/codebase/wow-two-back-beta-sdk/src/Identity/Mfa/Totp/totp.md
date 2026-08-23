# WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp

> TOTP / HOTP via `Otp.NET`. Step, digit count and verification window come from `TotpOptions`;
> the defaults are RFC 6238 — 6 digits, a 30-second step, ±1 step.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp
```

## Usage

### Enroll

```csharp
builder.Services.AddTotp(o => o.Digits = 8);   // omit the delegate for the RFC defaults

var secret = totp.GenerateSecret();
var uri = totp.BuildOtpAuthUri("MyApp", user.Email, secret);
// Render `uri` as QR code (e.g. via QRCoder), persist `secret` for the user.
```

### Verify

```csharp
// ITotpService is injected; it verifies against the registered parameters.
if (!totp.VerifyCode(user.TotpSecret, providedCode))
    throw new UnauthorizedAccessException("Invalid TOTP.");
```

## See also

- [Otp.NET](https://github.com/kspearrin/Otp.NET)
- [RFC 6238](https://datatracker.ietf.org/doc/html/rfc6238)
