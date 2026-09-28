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
| `POST login[?useCookies=true]` | bearer `{ accessToken, expiresIn, refreshToken }` or a cookie; `twoFactorCode` / `recoveryCode` / `twoFactorMethod` in the body |
| `POST refresh` · `POST logout` | rotate the refresh token · revoke it and clear the cookie |
| `GET confirm-email?userId&token` | confirms the email |
| `POST resend-confirmation-email` · `POST forgot-password` | always `204`, so they reveal no accounts |
| `POST reset-password` | sets a new password with the emailed token |
| `GET manage/info` | the signed-in account (authorized) |
| `GET manage/2fa` · `POST manage/2fa/authenticator` · `enable` · `disable` · `recovery-codes` | two-factor setup; mapped only with `.AddTwoFactor()` |
| `POST manage/2fa/methods/{method}/send` · `…/{method}/enable` · `manage/2fa/preferred` | delivered-code methods: send, enable with the code, choose the first method |
| `POST email-sign-in/send` · `POST email-sign-in[?useCookies=true]` | passwordless link or code; mapped only with `.AddEmailSignIn()` |
| `POST login/two-factor[?useCookies=true]` | finishes a first factor that answered with a ticket; mapped with `.AddTwoFactor()` |
| `POST passkeys/login/options` · `POST passkeys/login[?useCookies=true]` | usernameless passkey sign-in; mapped only with `.AddPasskeys()` |
| `GET manage/passkeys` · `POST manage/passkeys/options` · `POST manage/passkeys` · `DELETE manage/passkeys/{id}` | list, start, finish and remove registrations |

- Success bodies use the `ApiResponse<T>` envelope (`{ data }`); failures are problem details with identity codes,
  translated when error translation is enabled.
- Lockout answers `429`, an unconfirmed account `403`, wrong credentials and a missing second factor `401`.
- Refresh renews without a second factor but re-checks lockout and confirmation; a refused renewal revokes the token.
- Without an `IEmailBroker` or a link template, emails are skipped and `confirmationSent` is false.

## Two-factor login

- A login that needs the second factor answers `401` with `twoFactorMethod`, `twoFactorMethods` and, for a delivered
  method, `codeSent` / `codeExpiresAt` as problem extensions; `AutoSendCode` sends the preferred method's code then.
- Resend or switch: repeat the login with `twoFactorMethod` and no code. Complete: repeat it with `twoFactorCode`.

## Passkeys

- `options` holds WebAuthn JSON for `PublicKeyCredential.parseCreationOptionsFromJSON` / `parseRequestOptionsFromJSON`.
- Send back `{ state, credential: credential.toJSON() }`; registration also takes an optional `name`.
- A failed or replayed passkey login answers `401`; lockout `429` and unconfirmed `403`, as for passwords.
