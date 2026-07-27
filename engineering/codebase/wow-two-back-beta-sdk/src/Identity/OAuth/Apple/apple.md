# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Apple

> Sign in with Apple OAuth provider via `AspNet.Security.OAuth.Apple`. Scheme `Apple`.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Apple
```

## Usage

```csharp
builder.Services
    .AddAuthentication()
    .AddAppleAuthentication(
        clientId: builder.Configuration["OAuth:Apple:ClientId"]!,
        teamId: builder.Configuration["OAuth:Apple:TeamId"]!,
        keyId: builder.Configuration["OAuth:Apple:KeyId"]!,
        privateKeyPath: "/secrets/AuthKey_XXXXXXXXXX.p8");
```

## Baseline

`SaveTokens=true` + scope merge + `wt:provider` stamp applied automatically — see [`../oauth.md`](../oauth.md).

## Quirk

- Departs from the uniform signature: requires `teamId`, `keyId`, `privateKeyPath` (the `.p8` developer key) in addition to `clientId`.
- Apple posts the callback as `form_post`; name/email arrive only on the **first** authorization.
