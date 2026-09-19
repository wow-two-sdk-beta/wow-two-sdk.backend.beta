# WoW.Two.Sdk.Backend.Beta.Identity.Jwt

> JWT bearer authentication with issuer, audience, lifetime, algorithm and one verification-key source enforced.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.Jwt
```

## Usage

### Symmetric key

```csharp
builder.Services.AddJwtBearerAuthentication(o =>
{
    o.Issuer = "https://my-issuer";
    o.Audience = "my-api";
    o.SymmetricKey = builder.Configuration["Jwt:Key"]!;
    o.Algorithm = SecurityAlgorithms.HmacSha256;
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
```

### OpenID Connect metadata

```csharp
builder.Services.AddJwtBearerAuthentication(o =>
{
    o.Issuer = "https://login.microsoftonline.com/{tenant}/v2.0";
    o.Audience = "api://my-api";
    o.MetadataAddress = new Uri("https://login.microsoftonline.com/{tenant}/v2.0/.well-known/openid-configuration");
    o.Algorithm = SecurityAlgorithms.RsaSha256;
});
```

Configure exactly one of `SymmetricKey` and `MetadataAddress`. Remote metadata requires HTTPS. A local HTTP identity provider requires the explicit `AllowInsecureMetadataForDevelopment` escape.
