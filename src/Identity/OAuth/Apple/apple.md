# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Apple

> Sign in with Apple OAuth provider via `AspNet.Security.OAuth.Apple`.

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
