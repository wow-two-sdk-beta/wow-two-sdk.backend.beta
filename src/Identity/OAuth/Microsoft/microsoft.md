# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Microsoft

> Microsoft Account / Entra ID OAuth provider.

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

For full Entra ID integration (Graph API, OBO, etc.), use `Microsoft.Identity.Web` directly instead.
