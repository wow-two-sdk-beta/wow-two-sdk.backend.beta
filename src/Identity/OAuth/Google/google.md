# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google

> Google sign-in OAuth provider via built-in `Microsoft.AspNetCore.Authentication.Google`. Scheme `Google`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.Cookies
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google
```

## Usage

```csharp
builder.Services
    .AddCookieAuthentication()
    .AddAuthentication()
    .AddGoogleAuthentication(
        builder.Configuration["OAuth:Google:ClientId"]!,
        builder.Configuration["OAuth:Google:ClientSecret"]!,
        scopes: ["https://www.googleapis.com/auth/calendar.readonly"]);
```

## Baseline

`SaveTokens=true` + scope merge + `wt:provider` stamp applied automatically — see [`../oauth.md`](../oauth.md).

## Quirk

- Uses the built-in `GoogleOptions` (not an aspnet-contrib package).
