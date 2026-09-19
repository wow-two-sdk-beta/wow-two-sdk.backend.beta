# WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google

> Two Google sign-in flows: **redirect** (server-side challenge → `Google` scheme cookie) and **ID-token authentication** (SPA obtains a token client-side, backend validates it).

## Flows

| Flow | Use | Entry point |
|---|---|---|
| Redirect | server-rendered apps; the backend owns the OAuth handshake | `AddGoogleAuthentication` (on `AuthenticationBuilder`) |
| ID-token authentication | SPA / mobile signs in with Google's JS SDK and POSTs the ID token; backend trusts it | `AddGoogleIdTokenAuthenticator` (on `IServiceCollection`) |

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

## ID-token authentication (SPA flow)

```csharp
builder.Services.AddGoogleIdTokenAuthenticator(o => o
    .WithClientId(builder.Configuration["OAuth:Google:ClientId"]!));

// in the sign-in endpoint
var identity = await _authenticator.AuthenticateAsync(body.IdToken, ct);   // null ⇒ untrusted ⇒ 401
if (identity is null) return Results.Unauthorized();
// identity.Subject / .Email / .Name / .Picture
```

- Wraps `Google.Apis.Auth` `GoogleJsonWebSignature.ValidateAsync` — checks signature, audience, expiry.
- `Audiences` MUST be the OAuth client id(s) the SPA obtained its token for; a token with no email is rejected.
- `IGoogleIdTokenAuthenticator` is the testable seam — swap a fake in tests, never call Google.
- Caller cancellation stops awaiting validation. Google.Apis.Auth exposes no cancellation token, so an in-flight certificate refresh finishes independently.

## Quirk

- Redirect flow uses the built-in `GoogleOptions` (not an aspnet-contrib package).
