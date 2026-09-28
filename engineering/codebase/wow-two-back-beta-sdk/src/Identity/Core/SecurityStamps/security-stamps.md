# Identity.Core.SecurityStamps

Revocation through the security stamp: a principal whose stamp claim differs from the stored user's is rejected.
Password, email and explicit rotations (`UserAccountService.RotateSecurityStampAsync`) end every session.

## Quick start

```csharp
identity.AddSecurityStampValidation(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

services.AddCookieAuthentication();              // OnValidatePrincipal → RejectPrincipal + sign-out
services.AddJwtBearerAuthentication(o => …);     // OnTokenValidated → Fail
```

## Notes

- Hooks every cookie and bearer scheme through `PostConfigureAll`; existing event handlers still run first.
- Claim types come from `IdentityCoreOptions.Claims` (`UserIdClaimType`, `SecurityStampClaimType`).
- The stored stamp is cached per host for `ValidationInterval`; revocation reaches other hosts within that interval.
  Zero reads the user on every request.
- A principal without a user id (API key, guest) passes; one with an id but no stamp passes unless
  `RejectPrincipalsWithoutStamp` is set.
