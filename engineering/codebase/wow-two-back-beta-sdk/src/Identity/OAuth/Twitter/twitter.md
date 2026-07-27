# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Twitter

> X (Twitter) OAuth 2.0 provider with PKCE via `AspNet.Security.OAuth.Twitter`. Scheme `Twitter`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Twitter
```

## Usage

```csharp
builder.Services
    .AddAuthentication()
    .AddTwitterAuthentication(
        builder.Configuration["OAuth:Twitter:ClientId"]!,
        builder.Configuration["OAuth:Twitter:ClientSecret"]!);
```

Defaults to `tweet.read users.read` when no scopes are passed; supply scopes to override:

```csharp
.AddTwitterAuthentication(id, secret, scopes: ["tweet.read", "users.read", "offline.access"]);
```

## Baseline

`SaveTokens=true` + scope merge + `wt:provider` stamp applied automatically — see [`../oauth.md`](../oauth.md).

## Quirk

- OAuth **2.0 + PKCE** (the aspnet-contrib package) — **not** the built-in OAuth-1.0a `Microsoft.AspNetCore.Authentication.Twitter`.
- Email is rarely present in the profile; don't rely on it for account linking.
- `offline.access` is required to receive a refresh token.
