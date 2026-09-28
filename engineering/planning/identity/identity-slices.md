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
- [ ] I3 — lockout + security stamps: failure counting, lockout window, stamp rotation, cookie and JWT validation hooks
- [ ] I4 — roles + user claims + principal factory: repositories, services, `UserClaimsPrincipalFactory`
- [ ] I5 — sign-in: `SignInService` orchestrating password → lockout → confirmation → two-factor → principal
- [ ] I6 — external logins + stored tokens + two-factor: login repository, authenticator key, recovery codes

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
