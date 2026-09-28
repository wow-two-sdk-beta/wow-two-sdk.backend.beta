# Identity.Core.TwoFactor

Authenticator (TOTP) keys and single-use recovery codes, stored in `identity_user_tokens` under ASP.NET Identity's
provider name, so migrated authenticator keys keep working.

## Quick start

```csharp
identity.AddEntityFrameworkStores<AppDbContext>().AddTwoFactor(o => o.Issuer = "Acme");

var key = await twoFactor.ResetAuthenticatorKeyAsync(user);        // show key / QR of GetAuthenticatorUriAsync
await twoFactor.EnableAsync(user, codeFromApp);                     // proves the app holds the key
var codes = await twoFactor.GenerateRecoveryCodesAsync(user);       // show once
```

## Notes

- Recovery codes are stored as SHA-256 digests; redemption ignores case and the dash.
- Enabling, disabling and resetting the key rotate the security stamp; disabling forgets the key and codes.
- Two-factor sign-in (`SignInService`) also needs `.AddUserTokens(...)` for the ticket between the two steps.
