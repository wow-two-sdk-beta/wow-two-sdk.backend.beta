# WoW.Two.Sdk.Backend.Beta.Identity.Cookies

> Cookie authentication with secure defaults (HTTP-only, SameSite=Lax, HTTPS-only).

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.Cookies
```

## Usage

```csharp
builder.Services.AddCookieAuthentication(o =>
{
    o.ExpireTimeSpan = TimeSpan.FromHours(24);
    o.LoginPath = "/auth/login";
});
```
