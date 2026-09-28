# Identity.Core.EmailSignIn

Passwordless sign-in by email: a single-use link or a short code, both kept by the OTP service. It is a first factor,
so lockout, confirmation preconditions and two-factor still apply through `SignInService.SignInAsync`.

## Quick start

```csharp
identity.AddUserTokens(o => o.SigningKey = key).AddLockout().AddEmailSignIn().AddSignIn().AddAccountEndpoints();
```

```jsonc
"UserAccounts": { "Emails": { "MagicLink": "https://app.example/magic?email={email}&token={token}" } },
"Identity": { "EmailSignIn": { "LinkLifetime": "00:15:00", "CodeLifetime": "00:10:00", "CodeLength": 6 } }
```

| Route | Body | Answer |
|---|---|---|
| `POST email-sign-in/send` | `{ email, method: "link" \| "code" }` | always `204` |
| `POST email-sign-in[?useCookies=true]` | `{ email, token }` or `{ email, code }` | a session, or `401` |
| `POST login/two-factor[?useCookies=true]` | `{ userId, ticket, twoFactorCode \| recoveryCode }` | a session |

## Notes

- Unknown addresses get no email and the same `204`, so the endpoint reveals no accounts.
- Links carry a 12-character token; codes are numeric. Each works once, and a new send replaces the last.
- A used link or code confirms an unconfirmed address (`ConfirmEmail`, default on), since it proves ownership.
- For a two-factor account the answer is a `401` naming `userId` and `twoFactorTicket`; finish on `login/two-factor`.
- Across hosts, register a shared `IOtpRepository` so a code sent by one host verifies on another.
