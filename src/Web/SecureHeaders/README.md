# WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders

> OWASP-flavored secure-headers middleware. Wraps `NetEscapades.AspNetCore.SecurityHeaders` with API-shaped defaults.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders
```

## Usage

```csharp
var app = builder.Build();
app.UseOwaspSecureHeaders();
```

Headers applied: HSTS · X-Content-Type-Options · X-Frame-Options: DENY · Referrer-Policy · Permissions-Policy · Cross-Origin-{Opener,Embedder,Resource}-Policy · Server header removed.

## See also

- [NetEscapades.AspNetCore.SecurityHeaders](https://github.com/andrewlock/NetEscapades.AspNetCore.SecurityHeaders)
- [OWASP secure headers](https://owasp.org/www-project-secure-headers/)
