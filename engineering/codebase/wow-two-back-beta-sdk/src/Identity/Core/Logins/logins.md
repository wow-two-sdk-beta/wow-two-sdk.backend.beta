# Identity.Core.Logins

External logins: link a provider account (`LoginProvider` + `ProviderKey`) to a user and find the user behind it.
The OAuth handshake stays with `Identity/OAuth/*`; this slice only persists the link.

## Quick start

```csharp
identity.AddEntityFrameworkStores<AppDbContext>().AddExternalLogins().AddSignIn();

var user = await logins.FindByLoginAsync("Google", subject)
    ?? await CreateAndLinkAsync(subject);                          // host decides sign-up policy
var result = await signIn.SignInAsync(user);
```

## Notes

- A provider account links to one user; linking it elsewhere fails with `LoginAlreadyAssociated`.
- Unlinking rotates the security stamp.
