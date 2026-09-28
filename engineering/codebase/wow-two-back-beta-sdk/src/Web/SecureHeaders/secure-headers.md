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

## Cross-origin isolation

`SecureHeadersOptions` drops the opener or embedder policy for a host that a cross-origin popup or an un-CORP'd subresource would otherwise break. Both default on.

```csharp
app.UseOwaspSecureHeaders(headers => headers.EnableCrossOriginEmbedderPolicy = false);
```

A host whose pages open OAuth popups, such as Google Identity Services, keeps the opener policy but relaxes it to
`same-origin-allow-popups`:

```csharp
app.UseOwaspSecureHeaders(headers => headers.AllowOpenerPopups = true);
builder.AddApiDefaults(o => o.AllowCrossOriginOpenerPopups = true);   // the boot-floor equivalent
```

Under the `AddApiDefaults` boot floor the same two flags sit on `ApiDefaultsOptions` and are forwarded:

```csharp
builder.AddApiDefaults(o => o.EnableCrossOriginEmbedderPolicy = false);
```

## See also

- [NetEscapades.AspNetCore.SecurityHeaders](https://github.com/andrewlock/NetEscapades.AspNetCore.SecurityHeaders)
- [OWASP secure headers](https://owasp.org/www-project-secure-headers/)
