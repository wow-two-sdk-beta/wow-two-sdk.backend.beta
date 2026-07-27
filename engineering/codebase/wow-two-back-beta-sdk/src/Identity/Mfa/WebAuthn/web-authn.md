# WoW.Two.Sdk.Backend.Beta.Identity.Mfa.WebAuthn

> WebAuthn / FIDO2 / passkeys via `Fido2.AspNet`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.Mfa.WebAuthn
```

## Usage

```csharp
builder.Services.AddFido2WebAuthn(
    serverDomain: "example.com",
    serverName:   "MyApp",
    origins:      "https://example.com", "https://app.example.com");
```

Inject `IFido2` into your endpoints to drive registration + assertion ceremonies.

## See also

- [Fido2.NetLib](https://github.com/passwordless-lib/fido2-net-lib)
- [WebAuthn spec](https://www.w3.org/TR/webauthn-3/)
