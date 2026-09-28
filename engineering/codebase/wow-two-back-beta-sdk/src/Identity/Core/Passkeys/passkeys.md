# Identity.Core.Passkeys

Passkeys as an identity slice: WebAuthn credentials stored in `identity_passkeys`, usernameless sign-in through
`SignInService`, and management per user. Ceremonies run through Fido2 (MIT); the slice owns storage and state.

## Quick start

```csharp
identity.AddLockout()
        .AddPasskeys(o => { o.ServerDomain = "acme.com"; o.ServerName = "Acme"; o.Origins.Add("https://acme.com"); })
        .AddSignIn();                                     // after AddEntityFrameworkStores

// register (signed in)
var ceremony = await passkeys.BeginRegistrationAsync(user);            // OptionsJson → navigator.credentials.create
await passkeys.CompleteRegistrationAsync(user, ceremony.State, credentialJson, "Laptop");

// sign in (no user name)
var request = passkeys.BeginSignIn();                                  // OptionsJson → navigator.credentials.get
var result  = await signIn.PasskeySignInAsync(request.State, assertionJson);
```

```jsonc
"Identity": { "Passkeys": { "ServerDomain": "acme.com", "Origins": [ "https://acme.com" ], "CeremonyLifetime": "00:05:00" } }
```

## Notes

- The host section `Identity:Passkeys` is applied after code and reloads live; domain and one origin are required.
- Ceremony state is sealed by ASP.NET Data Protection; share the key ring across hosts.
- A sign-in ceremony completes once per passkey: the use is recorded only when the last use predates the ceremony.
  That stops replays even from synced passkeys, which report signature counter 0.
- Registration excludes the user's passkeys; a credential id registers once across all users.
- Passkey sign-in skips the second factor: the credential is phishing-resistant and verifies the user.
  Lockout and confirmation preconditions still apply. `RequireUserVerification` demands PIN or biometrics.
- The user handle is a SHA-256 of the user id, so authenticators store no account identifier.
- Failures return `InvalidPasskey` (built-in `ru` / `uz` texts); the account API maps passkey routes when registered.
- Existing databases need a migration for `identity_passkeys`.
