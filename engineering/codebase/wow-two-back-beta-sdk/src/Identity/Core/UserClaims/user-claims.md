# Identity.Core.UserClaims

Per-user claims (`UserClaimService`), carried by principals the factory builds once `.AddUserClaims()` is registered.

## Quick start

```csharp
identity.AddEntityFrameworkStores<AppDbContext>().AddUserClaims();

await userClaims.AddClaimsAsync(user, [new Claim("tier", "gold")]);
await userClaims.ReplaceClaimAsync(user, new Claim("tier", "gold"), new Claim("tier", "platinum"));   // rotates the stamp
```

## Notes

- Removing or replacing a claim rotates the security stamp; adding one takes effect at the next sign-in.
- Claims match by type and value; duplicates are allowed and removed together.
