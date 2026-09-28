# Identity.Core.Endpoints

The account HTTP API over the identity slices — the SDK's `MapIdentityApi`, built on our own user model.

## Quick start

```csharp
services.AddUserAccounts<AppUser>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddArgon2Passwords().AddUserTokens(o => o.SigningKey = key).AddEmailConfirmation()
        .AddLockout().AddRefreshTokens().AddSignIn()
        .AddAccountEndpoints();                              // email links + texts from configuration
services.AddJwtTokenIssuance(…).AddJwtBearerAuthentication(…);

app.MapGroup("/account").MapUserAccountEndpoints<AppUser>();
```

```jsonc
"UserAccounts": { "Emails": {
  "ConfirmationLink": "https://app.example/confirm-email?userId={userId}&token={token}",
  "ResetLink": "https://app.example/reset-password?email={email}&token={token}"
} }
```

| Route | Does |
|---|---|
| `POST register` | creates the account; sends the confirmation email when configured |
| `POST login[?useCookies=true]` | bearer `{ accessToken, expiresIn, refreshToken }` or a cookie; `twoFactorCode` / `recoveryCode` in the body |
| `POST refresh` · `POST logout` | rotate the refresh token · revoke it and clear the cookie |
| `GET confirm-email?userId&token` | confirms the email |
| `POST resend-confirmation-email` · `POST forgot-password` | always `204`, so they reveal no accounts |
| `POST reset-password` | sets a new password with the emailed token |
| `GET manage/info` | the signed-in account (authorized) |

- Success bodies use the `ApiResponse<T>` envelope (`{ data }`); failures are problem details with identity codes,
  translated when error translation is enabled.
- Lockout answers `429`, an unconfirmed account `403`, wrong credentials and a missing second factor `401`.
- Refresh renews without a second factor but re-checks lockout and confirmation; a refused renewal revokes the token.
- Without an `IEmailBroker` or a link template, emails are skipped and `confirmationSent` is false.
