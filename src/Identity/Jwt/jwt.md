# WoW.Two.Sdk.Backend.Beta.Identity.Jwt

> JWT bearer authentication with sane defaults — issuer/audience/signing key validation, JWKS support.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.Jwt
```

## Usage

### Symmetric key (dev / quick-start)

```csharp
builder.Services.AddJwtBearerAuthentication(o =>
{
    o.Issuer = "https://my-issuer";
    o.Audience = "my-api";
    o.SymmetricKey = builder.Configuration["Jwt:Key"]!;
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
```

### JWKS (production — Auth0, Entra ID, Cognito, etc.)

```csharp
builder.Services.AddJwtBearerAuthentication(o =>
{
    o.Issuer = "https://login.microsoftonline.com/{tenant}/v2.0";
    o.Audience = "api://my-api";
    o.JwksUri = new Uri("https://login.microsoftonline.com/{tenant}/v2.0/.well-known/openid-configuration");
});
```
