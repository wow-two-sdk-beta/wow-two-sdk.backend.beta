# Identity slices — build track

*Last updated: 2026-09-28*

> Iterations completing the own-identity build order (steps 2–9 of
> [identity-architecture.md](identity-architecture.md) §9) on top of the shipped `identity/core`.
> Step 10 (Haven.Auth dogfood) stays a consumer pass.

## Rulings — 2026-09-28

- Per-slice services, not one facade: `UserAccountService` stays the core; each `.AddX()` registers its own service.
  An absent slice is an unresolvable service, so a host fails at composition instead of at a `NotSupportedException`.
- Column-backed capabilities (password, email, lockout, stamp, phone) change the user row through `IUserRepository`;
  only table-backed capabilities (roles, user claims, logins, stored tokens) get their own repository.
- Purpose tokens are HMAC-SHA256 over purpose, user id, security stamp and expiry, keyed by configured material and
  timed by `TimeProvider`. They are stateless, and a stamp rotation voids every outstanding token.
- Password rules follow NIST SP 800-63B: length first; composition rules exist but default off.
- The breached-password check uses the Pwned Passwords k-anonymity range API with padding; it fails open by default.
- Security-stamp validation compares the principal's stamp with the stored one at most once per interval per user.

## Status

- [x] I1 — password slice: Argon2id via `IPasswordHasher<TUser>`, rules + breach validators, set/change/check; rehash upgrade
- [x] I2 — purpose tokens + email slice: `UserTokenIssuer`, email confirmation and change, password reset; 13 tests
- [x] I3 — lockout + security stamps: failure counting, lockout window, rotation; cookie + JWT revocation end to end
- [x] I4 — roles + user claims + principal factory: repositories, services, `UserClaimsPrincipalFactory`
- [x] I5 — sign-in: `SignInService` orchestrating lockout → password → preconditions → two-factor → principal
- [x] I6 — external logins + stored tokens + two-factor: login repository, authenticator key, hashed recovery codes
- [x] I7 — phone slice: set, OTP confirmation and phone sign-in over the shipped `IOtpService`
- [x] I8 — refresh tokens: rotation, reuse detection revoking the family, stamp binding, purge; 61 identity tests pass
- [x] I9 — account HTTP API: `MapUserAccountEndpoints` (register, login bearer/cookie, refresh, logout, email confirm,
  forgot/reset password, info); emails from `UserAccounts:Emails` configuration; end-to-end tests
- [x] I10 — passkeys: `identity_passkeys`, `UserPasskeyService`, usernameless `PasskeySignInAsync`, account routes;
  software ES256 authenticator tests incl. replay, forgery, phishing origin and expiry

---

## I1 — Password slice

- `.AddArgon2Passwords()` registers `UserPasswordService`, the rules validator and Argon2id via `TryAdd`.
- `.AddBreachedPasswordCheck()` adds a validator backed by `PwnedPasswordsClient`.
- Acceptance: rules reject short and composition-failing passwords; a breached password is rejected; change rotates
  the stamp; a rehash-needed verification upgrades the stored hash.

## I2 — Purpose tokens + email slice

- `.AddUserTokens(o => o.SigningKey = …)` registers `UserTokenIssuer`; lifetimes per purpose.
- `.AddEmailConfirmation()` registers `UserEmailService`: confirm, change with a token bound to the new address.
- Password reset joins `UserPasswordService` when tokens are registered.
- Acceptance: tokens reject tampering, the wrong purpose, another user, expiry and a rotated stamp.

## I3 — Lockout + security stamps

- `.AddLockout()` registers `UserLockoutService`; new users take `LockoutOptions.EnabledForNewUsers`.
- `.AddSecurityStampValidation()` hooks cookie `OnValidatePrincipal` and JWT `OnTokenValidated`.
- Acceptance: the Nth failure locks until the window ends; a rotated stamp rejects an existing cookie or bearer token.

## I4 — Roles, user claims, principal factory

- `.AddRoles<TRole>()` registers role and user-role repositories plus `RoleService` / `UserRoleService`.
- `.AddUserClaims()` registers the claim repository plus `UserClaimService`.
- `UserClaimsPrincipalFactory` emits id, name, email, stamp, roles and claims for whichever slices exist.

## I5 — Sign-in

- `.AddSignIn()` registers `SignInService`: one result carries success, lockout, unconfirmed, two-factor or failure.
- The session shape stays the host's choice: a cookie sign-in or `ITokenIssuer.Issue(principal.Claims)`.

## I6 — External logins, stored tokens, two-factor

- `.AddExternalLogins()` registers the login repository plus `UserLoginService`.
- `.AddTwoFactor()` registers stored tokens, the TOTP authenticator key and hashed recovery codes.
- Two-factor sign-in carries the password-verified user in a 5-minute purpose-token ticket plus the user id.

## I8 — Refresh tokens

- `.AddRefreshTokens()` registers `RefreshTokenService` over `identity_refresh_tokens` (digest-only storage).
- Acceptance: rotation spends the token; a spent token revokes the family; expiry, tampering and stamp rotation fail.

## I7 — Phone slice

- `.AddPhoneNumbers()` registers `UserPhoneService`: set (unconfirmed), send and verify OTP codes, sign in by phone.
- Acceptance: a verified code confirms the number; a phone sign-in code resolves the user for `SignInAsync`.

## I10 — Passkeys

- `.AddPasskeys(o => …)` over the module-options recipe (`Identity:Passkeys`); Fido2 verifies, the slice stores.
- Ceremony state: Data Protection seal of kind, subject, start (whole milliseconds) and the options JSON.
- Single use without a store: `RecordUseAsync` updates only while `LastUsedAt < ceremony start`, atomically.
- Sign-in is usernameless (empty allow-list); the credential id finds the passkey, the user handle must match.
- Acceptance: register → list → sign in; a replayed or older ceremony, a forged signature, a foreign origin, an
  unknown credential, another user's or an expired state all fail; lockout still applies; owners alone remove.
