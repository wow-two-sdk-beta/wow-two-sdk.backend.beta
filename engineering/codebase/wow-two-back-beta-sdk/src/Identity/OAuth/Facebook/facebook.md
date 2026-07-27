# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Facebook

> Facebook OAuth provider via built-in `Microsoft.AspNetCore.Authentication.Facebook`. Scheme `Facebook`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Facebook
```

## Usage

```csharp
builder.Services
    .AddAuthentication()
    .AddFacebookAuthentication(
        builder.Configuration["OAuth:Facebook:AppId"]!,
        builder.Configuration["OAuth:Facebook:AppSecret"]!,
        scopes: ["email", "public_profile"]);
```

Profile fields are configured separately from scopes:

```csharp
.AddFacebookAuthentication(appId, appSecret, o => o.Fields.Add("picture"), "email");
```

## Baseline

`SaveTokens=true` + scope merge + `wt:provider` stamp applied automatically — see [`../oauth.md`](../oauth.md).

## Quirk

- `clientId`/`clientSecret` map to `AppId`/`AppSecret`.
- **`Fields` ≠ scopes** — extra profile fields are added via `o.Fields`, not the scopes param.
