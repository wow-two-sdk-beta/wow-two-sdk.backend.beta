# Identity.Core.Tokens

Stateless purpose tokens: `UserTokenIssuer` signs expiry + purpose + user id + security stamp with HMAC-SHA256.
Nothing is stored; rotating the stamp voids every outstanding token.

## Quick start

```csharp
identity.AddUserTokens(o =>
{
    o.SigningKey = cfg["Identity:TokenKey"];                     // base64, ≥ 32 bytes, same on every host
    o.Lifetimes[UserTokenPurposeConstants.PasswordReset] = TimeSpan.FromMinutes(30);
});

var token = issuer.Issue(user, "Invite");
var valid = issuer.Verify(user, "Invite", token);
```

## Notes

- A purpose may be scoped as `{purpose}:{value}`; the lifetime comes from the bare purpose.
- Built-in purposes (`UserTokenPurposeConstants`): email confirmation 3 days, password reset 2 hours, email change 1 day.
- Tokens are URL-safe base64 (55 characters) and verified in constant time.
