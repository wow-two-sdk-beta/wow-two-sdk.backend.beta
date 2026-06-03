# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google

> Google sign-in OAuth provider.

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
        builder.Configuration["OAuth:Google:ClientSecret"]!);
```
