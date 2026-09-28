# Identity.Core.Emails

Email slice: confirm the current address and move to a new one with a token sent to that address.
Needs `.AddUserTokens(...)`.

## Quick start

```csharp
identity.AddUserTokens(o => o.SigningKey = key).AddEmailConfirmation();

var confirm = emails.IssueConfirmationToken(user);                  // scoped to the current address
await emails.ConfirmEmailAsync(user, confirm);

var change = emails.IssueChangeEmailToken(user, "new@x.io");        // send to the new address
await emails.ChangeEmailAsync(user, "new@x.io", change);            // confirms it, rotates the stamp
```

## Notes

- A confirmation token dies when the address changes; a change token only moves to the address it names.
- `SetEmailAsync` sets an unconfirmed address directly for administration or sign-up corrections.
- Uniqueness follows `IdentityCoreOptions.User.RequireUniqueEmail`; the account's own address never counts as taken.
