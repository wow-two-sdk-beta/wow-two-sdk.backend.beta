# Identity.Core.RefreshTokens

Rotating refresh tokens for JWT sessions (OAuth 2.0 security BCP): redeeming a token spends it and issues its
replacement; presenting a spent token again revokes its whole family.

## Quick start

```csharp
identity.AddEntityFrameworkStores<AppDbContext>().AddRefreshTokens(o => o.Lifetime = TimeSpan.FromDays(30)).AddSignIn();

// sign-in: access token + refresh token
var refresh = await refreshTokens.IssueAsync(user);

// refresh endpoint
var redeemed = await refreshTokens.RedeemAsync(request.RefreshToken);
if (!redeemed.Succeeded) return Unauthorized();
var signIn = await signInService.SignInAsync(redeemed.User!);          // lockout + preconditions still apply
return new { access = issuer.Issue(signIn.Principal!.Claims), refresh = redeemed.Token!.Token };
```

## Notes

- Tokens are `{id}.{secret}`; only the secret's SHA-256 digest is stored (`identity_refresh_tokens`).
- A security-stamp rotation voids every refresh token of the user; `RevokeAsync` ends one family (sign-out),
  `RevokeAllAsync` every device.
- Consumption is one conditional update; two concurrent refreshes of one token count as reuse, so clients serialize them.
- Each rotation starts a new `Lifetime` window; the session slides while it stays in use.
- `PurgeExpiredAsync` deletes expired rows; call it from a recurring job.
- SQLite hosts apply `ApplyDateTimeOffsetToBinaryConversion()` so expiry comparisons translate.
