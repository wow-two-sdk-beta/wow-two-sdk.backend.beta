# Identity.Claims

Provider-agnostic **claim normalizer**: downstream code reads ONE canonical `wt:*` set regardless of OAuth provider. A per-request `IClaimsTransformation` reads `wt:provider` (sign-in scheme, stamped by the OAuth wrappers), looks up that provider's profile, and copies/derives `wt:*` from the raw claims already on the principal. Idempotent (early-returns if `wt:user_id` present); never fabricates.

```csharp
builder.Services.AddClaimNormalization();   // opt-in, after OAuth + cookie registration

// downstream — canonical claims off the principal (null when unsupplied):
var id   = User.GetUserId();
var name = User.GetDisplayName();
var who  = User.GetUsername();   // handle; authorization leaf reads this
```

## Normalized claim set

| Canonical const | Claim type | Meaning |
|---|---|---|
| `NormalizedClaimTypes.Provider` | `wt:provider` | Sign-in scheme name (read; stamped upstream) |
| `NormalizedClaimTypes.UserId` | `wt:user_id` | Provider-stable user id |
| `NormalizedClaimTypes.Email` | `wt:email` | Email, when supplied |
| `NormalizedClaimTypes.DisplayName` | `wt:display_name` | Human display name, when supplied |
| `NormalizedClaimTypes.Username` | `wt:username` | Provider handle / login, when supplied |
| `NormalizedClaimTypes.Avatar` | `wt:avatar` | Avatar URL — copied or synthesized |

Each field resolves from an **ordered set** of source claim types (first match wins): OAuth2 long `ClaimTypes.*` URIs and OIDC short names (`sub`, `email`, `name`, `preferred_username`, `picture`) are both accepted.

> ⚠️ `ClaimTypes.Name` is a **handle** for GitHub / GitLab / Twitch / Discord / Twitter / Reddit / Yandex, but a **display name** for Google / Microsoft / Facebook / Spotify / LinkedIn / Amazon. The per-provider profile decides which `wt:*` field it feeds — never treated uniformly.

## Per-provider availability

Source: provider library `ClaimActions`. ✅ filled · ∅ provider can't supply · ⚙ synthesized · ⚠ verify (best-effort).

| Provider | user_id | email | display_name | username | avatar |
|---|---|---|---|---|---|
| GitHub | ✅ `NameIdentifier` | ✅ `Email` | ✅ `urn:github:name` | ✅ `Name` (login) | ⚙ `…/u/{id}` |
| Google | ✅ `NameIdentifier`/sub | ✅ `Email` | ✅ `Name` | ∅ | ✅ `urn:google:picture` / ∅ |
| Microsoft | ✅ `oid`/`NameIdentifier` | ✅ `Email`/`mail`/`preferred_username` | ✅ `Name` | ✅ `preferred_username` / ∅ | ∅ |
| Facebook | ✅ `NameIdentifier` | ✅ `Email` | ✅ `Name` | ∅ | ∅ |
| Apple | ✅ `NameIdentifier`/sub | ✅ `Email` | ✅ `Name` (1st consent) | ∅ | ∅ |
| Twitter/X | ✅ `NameIdentifier` | ⚠ `Email` (usually ∅) | ✅ `urn:…:name`/`Name` | ✅ `Name` (handle) | ⚠ URL claim if present |
| Discord | ✅ `NameIdentifier` | ✅ `Email` | ∅ | ✅ `Name` (username) | ⚙ `cdn…/{id}/{hash}.png` |
| GitLab | ✅ `NameIdentifier` | ✅ `Email` | ✅ `urn:gitlab:name` | ✅ `Name` (handle) | ✅ `urn:gitlab:avatar` |
| Twitch | ✅ `NameIdentifier` | ✅ `Email` | ✅ `urn:twitch:displayname` | ✅ `Name` (login) | ✅ `urn:twitch:profileimageurl` |
| Spotify | ✅ `NameIdentifier` | ✅ `Email` | ✅ `Name` (display_name) | ∅ | ✅ `urn:spotify:profilepicture` |
| LinkedIn | ✅ `NameIdentifier`/sub | ✅ `Email` | ✅ `Name` | ∅ | ⚠ `picture` if present |
| Amazon | ⚠ `NameIdentifier` | ⚠ `Email` | ⚠ `Name` | ∅ | ∅ |
| Reddit | ⚠ `NameIdentifier` | ⚠ `Email` (∅ default) | ∅ | ⚠ `Name` (handle) | ∅ |
| Yandex | ⚠ `NameIdentifier` | ⚠ `Email` | ⚠ `urn:…`/`Name` | ⚠ `Name` (login) | ∅ |
| Vkontakte | ⚠ `NameIdentifier` | ⚠ `Email` (scoped) | ⚠ `Name` | ∅ | ⚠ photo claim |
| Slack | ⚠ `NameIdentifier` | ⚠ `Email` | ⚠ `Name` | ∅ | ⚠ `picture` |
| Notion | ⚠ `NameIdentifier` | ⚠ `Email` | ⚠ `Name` | ∅ | ⚠ `picture` |

## Seam

- **Upstream** (`Identity/OAuth/*`): each wrapper stamps `new Claim("wt:provider", context.Scheme.Name)` at sign-in. This leaf only **reads** it.
- **Downstream** (authorization leaf): reads `wt:username` via `ClaimsPrincipalExtensions.GetUsername()`.

## See also

- Contract: [`ClaimNormalization.standard.md`](./ClaimNormalization.standard.md) — guaranteed vs provider-dependent fields (RFC 2119).
- API + examples: [`ClaimNormalization.spec.md`](./ClaimNormalization.spec.md).
- Underlying lib: [AspNet.Security.OAuth.Providers](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers).
