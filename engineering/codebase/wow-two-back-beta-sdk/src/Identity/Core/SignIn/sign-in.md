# Identity.Core.SignIn

`SignInService` orchestrates lockout → password → preconditions → second factor → principal. The result carries a
`ClaimsPrincipal`; the host chooses the session shape.

## Quick start

```csharp
identity.AddArgon2Passwords().AddLockout().AddUserTokens(o => o.SigningKey = key).AddTwoFactor().AddSignIn();

var result = await signIn.PasswordSignInAsync(request.Login, request.Password);
switch (result.Status)
{
    case SignInStatus.Succeeded:
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, result.Principal!);   // cookie
        // or: tokenIssuer.Issue(result.Principal!.Claims)                                             // JWT
        break;
    case SignInStatus.RequiresTwoFactor:
        return new { result.UserId, result.TwoFactorTicket };      // client returns both with the code
}

await signIn.TwoFactorSignInAsync(userId, ticket, code);          // or RecoveryCodeSignInAsync
```

## Notes

- Unknown logins spend one hash verification, so response time does not reveal which accounts exist.
- Wrong passwords and wrong second factors both count toward lockout; the failure count resets only on success.
- A user with `TwoFactorEnabled` always gets `RequiresTwoFactor`, even when the two-factor slice is absent.
- The two-factor ticket is a purpose token (5 minutes by default) bound to the user and stamp.
- `SignInAsync(user)` completes external-login and passwordless flows with the same lockout and precondition checks.
- Preconditions: `IdentityCoreOptions.SignIn` (`RequireConfirmedEmail`, `RequireConfirmedPhoneNumber`, `AllowEmailLogin`).
