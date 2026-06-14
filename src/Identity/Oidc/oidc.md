# WoW.Two.Sdk.Backend.Beta.Identity.Oidc

> OpenID Connect authentication — Authorization Code + PKCE — paired with cookie auth.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.Cookies
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.Oidc
```

## Usage

```csharp
builder.Services.AddCookieAuthentication();
builder.Services.AddOpenIdConnectAuthentication(o =>
{
    o.Authority = "https://login.microsoftonline.com/{tenant}/v2.0";
    o.ClientId  = "<app-id>";
    o.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
    o.Scopes = ["openid", "profile", "email", "offline_access"];
});
```

Works with any standards-compliant OIDC provider — Auth0, Okta, Keycloak, Microsoft Entra ID, AWS Cognito, GCP Identity Platform, Authentik, Zitadel, …
