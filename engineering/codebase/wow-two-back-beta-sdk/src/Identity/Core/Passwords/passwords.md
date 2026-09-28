# Identity.Core.Passwords

Password slice over the core user row: rules, optional breach check, set / change / remove / check, token reset.
Every stored password rotates the security stamp, ending older sessions and purpose tokens.

## Quick start

```csharp
services.AddUserAccounts<AppUser>(o => o.Password.MinLength = 12)
        .AddEntityFrameworkStores<AppDbContext>()
        .AddArgon2Passwords()               // rules + Argon2id (a hasher registered earlier wins)
        .AddBreachedPasswordCheck()         // optional: Pwned Passwords k-anonymity range query
        .AddUserTokens(o => o.SigningKey = cfg["Identity:TokenKey"]);   // enables reset

var result = await passwords.CreateWithPasswordAsync(user, "correct horse battery");
var ok     = await passwords.CheckPasswordAsync(user, input);            // upgrades outdated hashes
var token  = passwords.IssuePasswordResetToken(user);                     // mail it
await passwords.ResetPasswordAsync(user, token, newPassword);
```

## Notes

- Rules follow NIST SP 800-63B: `MinLength` 8, `MaxLength` 128, composition rules off, account identifiers rejected.
- Validators run in registration order and report every failure; add one by registering `IUserPasswordValidator<TUser>`.
- The breach check sends only five SHA-1 hex characters, asks for padded responses and fails open by default
  (`FailOpen = false` rejects the password when the corpus is unreachable).
- `CheckPasswordAsync` counts no failures; lockout belongs to the sign-in slice.
- Argon2id verifies with the cost recorded in each hash and re-hashes a match recorded with other parameters.
