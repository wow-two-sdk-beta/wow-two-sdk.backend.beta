# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google

> Two Google sign-in flows: **redirect** (server-side challenge → `Google` scheme cookie) and **ID-token verify** (SPA obtains a token client-side, backend validates it).

## Flows

| Flow | Use | Entry point |
|---|---|---|
| Redirect | server-rendered apps; the backend owns the OAuth handshake | `AddGoogleAuthentication` (on `AuthenticationBuilder`) |
| ID-token verify | SPA / mobile signs in with Google's JS SDK and POSTs the ID token; backend trusts it | `AddGoogleIdTokenVerifier` (on `IServiceCollection`) |

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
        builder.Configuration["OAuth:Google:ClientSecret"]!,
        scopes: ["https://www.googleapis.com/auth/calendar.readonly"]);
```

## Baseline (redirect flow)

`SaveTokens=true` + scope merge + `wt:provider` stamp applied automatically — see [`../oauth.md`](../oauth.md).

## ID-token verify (SPA flow)

```csharp
builder.Services.AddGoogleIdTokenVerifier(o => o
    .WithClientId(builder.Configuration["OAuth:Google:ClientId"]!));

// in the sign-in endpoint
var identity = await _verifier.VerifyAsync(body.IdToken, ct);   // null ⇒ untrusted ⇒ 401
if (identity is null) return Results.Unauthorized();
// identity.Subject / .Email / .Name / .Picture
```

- Wraps `Google.Apis.Auth` `GoogleJsonWebSignature.ValidateAsync` — checks signature, audience, expiry.
- `Audiences` MUST be the OAuth client id(s) the SPA obtained its token for; a token with no email is rejected.
- `IGoogleIdTokenVerifier` is the testable seam — swap a fake in tests, never call Google.

## Quirk

- Redirect flow uses the built-in `GoogleOptions` (not an aspnet-contrib package).
