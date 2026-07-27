# WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp

> TOTP / HOTP helpers (RFC 6238 / 4226) via `Otp.NET`. 6-digit codes, 30-second step, ±1 step tolerance.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp
```

## Usage

### Enroll

```csharp
var secret = TotpService.GenerateSecret();
var uri = TotpService.BuildOtpAuthUri("MyApp", user.Email, secret);
// Render `uri` as QR code (e.g. via QRCoder), persist `secret` for the user.
```

### Verify

```csharp
if (!TotpService.VerifyCode(user.TotpSecret, providedCode))
    throw new UnauthorizedAccessException("Invalid TOTP.");
```

## See also

- [Otp.NET](https://github.com/kspearrin/Otp.NET)
- [RFC 6238](https://datatracker.ietf.org/doc/html/rfc6238)
