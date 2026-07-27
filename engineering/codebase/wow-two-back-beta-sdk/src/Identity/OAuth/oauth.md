# WoW.Two.Sdk.Backend.Beta.Identity.OAuth

> Provider-baseline standard. Every OAuth provider wrapper conforms to one shape, applies one baseline, and stamps the originating scheme so downstream claim normalization is provider-agnostic.

## Uniform signature

Every provider exposes exactly one extension with this shape:

```csharp
public static AuthenticationBuilder Add{Provider}Authentication(
    this AuthenticationBuilder auth,
    string clientId,
    string clientSecret,
    Action<{ProviderOptions}>? configure = null,
    params string[] scopes)
```

- `clientId` / `clientSecret` guarded by `ArgumentException.ThrowIfNullOrWhiteSpace`; `auth` by `ArgumentNullException.ThrowIfNull`.
- Apple is the only departure — it carries required extras: `(auth, clientId, teamId, keyId, privateKeyPath, configure, scopes)`.
- Facebook maps `clientId`→`AppId`, `clientSecret`→`AppSecret` (its options use app-id naming).

## Baseline contract (`OAuthBaseline`)

Applied inside every wrapper's options delegate, operating on base `OAuthOptions`. Order is fixed:

```csharp
auth.Add{X}(o =>
{
    o.ClientId = clientId; o.ClientSecret = clientSecret;   // (or AppId/AppSecret)
    OAuthBaseline.ApplyBaseline(o, scopes);                 // 1. SaveTokens + merge scopes
    configure?.Invoke(o);                                   // 2. host customization
    OAuthBaseline.StampProvider(o);                         // 3. stamp LAST — uncroppable
});
```

1. **`SaveTokens = true`** — access/refresh tokens persisted into the auth properties.
2. **Scopes** — each passed scope merged into `o.Scope` (deduped; blank scopes skipped).
3. **Provider stamp** — `OnCreatingTicket` is wrapped *after* `configure` so the `wt:provider` claim runs after any host-supplied handler and **cannot be dropped**. The stamp chains the pre-existing handler, then adds `new Claim("wt:provider", context.Scheme.Name)` to `context.Identity`.

We do **not** add any `ClaimActions` / claim mapping here. Claim normalization is owned by the `Identity/Claims` leaf, which reads the `wt:provider` stamp via `NormalizedClaimTypes.Provider` (`"wt:provider"`).

## Conformance checklist — adding a provider

1. Folder `src/Identity/OAuth/{Provider}/` (PascalCase, matches namespace segment).
2. One static class `{Provider}OAuthServiceCollectionExtensions` with the uniform signature above.
3. Guards: `ThrowIfNull(auth)` + `ThrowIfNullOrWhiteSpace(clientId, clientSecret)`.
4. Delegate order: set credentials → `ApplyBaseline(o, scopes)` → `configure?.Invoke(o)` → `StampProvider(o)`.
5. No `ClaimActions` / claim mapping (that's `Identity/Claims`' job).
6. XML `<summary>` on the class and method (+ `<param>` on each) — zero warnings (`TreatWarningsAsErrors`).
7. Add the NuGet version line to `src/Directory.Packages.props` (match the sibling `AspNet.Security.OAuth.*` version).
8. Add a row to the table below + a `{provider}.md` folder doc.

## Providers

| Provider | Scheme | NuGet package | Default scopes | Quirk |
|---|---|---|---|---|
| GitHub | `GitHub` | `AspNet.Security.OAuth.GitHub` | — | `user:email` needed for verified email |
| Google | `Google` | `Microsoft.AspNetCore.Authentication.Google` | — | built-in `GoogleOptions` |
| Microsoft | `Microsoft` | `Microsoft.AspNetCore.Authentication.MicrosoftAccount` | — | tenant endpoints via `configure`; for full Entra/Graph use `Microsoft.Identity.Web` |
| Facebook | `Facebook` | `Microsoft.AspNetCore.Authentication.Facebook` | — | `Fields` ≠ scopes — set `o.Fields` in `configure` |
| Apple | `Apple` | `AspNet.Security.OAuth.Apple` | — | `.p8` private key; `form_post` response mode; extra `teamId`/`keyId`/`privateKeyPath` args |
| X (Twitter) | `Twitter` | `AspNet.Security.OAuth.Twitter` | `tweet.read users.read` | OAuth2 **+ PKCE** (not OAuth-1.0a); email rarely present |
| Amazon | `Amazon` | `AspNet.Security.OAuth.Amazon` | — | — |
| Discord | `Discord` | `AspNet.Security.OAuth.Discord` | — | — |
| GitLab | `GitLab` | `AspNet.Security.OAuth.GitLab` | — | self-managed: set host via `configure` |
| LinkedIn | `LinkedIn` | `AspNet.Security.OAuth.LinkedIn` | — | OpenID scopes (`openid profile email`) |
| Notion | `Notion` | `AspNet.Security.OAuth.Notion` | — | workspace-scoped tokens |
| Reddit | `Reddit` | `AspNet.Security.OAuth.Reddit` | — | refresh requires `duration=permanent` |
| Slack | `Slack` | `AspNet.Security.OAuth.Slack` | — | user vs bot scopes differ |
| Spotify | `Spotify` | `AspNet.Security.OAuth.Spotify` | — | — |
| Twitch | `Twitch` | `AspNet.Security.OAuth.Twitch` | — | — |
| Vkontakte | `Vkontakte` | `AspNet.Security.OAuth.Vkontakte` | — | — |
| Yandex | `Yandex` | `AspNet.Security.OAuth.Yandex` | — | — |

## See also

- `Identity/Claims` — the normalized-claim contract; consumes the `wt:provider` stamp via `NormalizedClaimTypes.Provider`.
- `Identity/Cookies` — pair `AddCookieAuthentication` with any provider for the sign-in cookie.
- Underlying libs: [aspnet-contrib OAuth providers](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers).
