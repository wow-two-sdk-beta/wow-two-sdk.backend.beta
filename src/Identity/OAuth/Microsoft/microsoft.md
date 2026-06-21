# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Microsoft

> Microsoft Account / Entra ID OAuth provider via built-in `Microsoft.AspNetCore.Authentication.MicrosoftAccount`. Scheme `Microsoft`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Microsoft
```

## Usage

```csharp
builder.Services
    .AddAuthentication()
    .AddMicrosoftAuthentication(
        builder.Configuration["OAuth:Microsoft:ClientId"]!,
        builder.Configuration["OAuth:Microsoft:ClientSecret"]!);
```

Single-tenant — point the endpoints at your tenant via `configure`:

```csharp
.AddMicrosoftAuthentication(id, secret, o =>
{
    o.AuthorizationEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize";
    o.TokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
});
```

## Baseline

`SaveTokens=true` + scope merge + `wt:provider` stamp applied automatically — see [`../oauth.md`](../oauth.md).

## Quirk

- Multi-tenant by default. For full Entra ID integration (Graph API, OBO), use `Microsoft.Identity.Web` directly instead.
