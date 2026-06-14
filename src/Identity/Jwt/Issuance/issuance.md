# Identity.Jwt.Issuance

JWT **issuance** (`ITokenIssuer`) — the sibling of `Identity/Jwt` which only **validates**.
Symmetric HMAC (HS256/384/512) via `JsonWebTokenHandler`. Pure library: workers and console
apps can issue tokens without pulling ASP.NET auth middleware.

```csharp
builder.Services.AddJwtTokenIssuance(o =>
{
    o.Issuer     = "my-auth";
    o.Audience   = "authenticated";
    o.Lifetime   = TimeSpan.FromHours(24);
    o.SigningKey = config["Auth:JwtKey"]!;   // ≥ 32 bytes; source from a secret store
});

var token = _tokens.Issue(
    new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id), new Claim("user_role", user.Role) },
    new TokenIssuanceContext(Lifetime: TimeSpan.FromHours(24)));
```

- Consumer constructs the claims — no user model in the SDK.
- Same key in `AddJwtBearerAuthentication` ⇒ tokens validate across services (Supabase-style shared-secret setups included).
- Not in v1: refresh tokens, revocation, asymmetric keys, key rotation (`IKeyProvider` is the v2 seam).
