# Authorization — standard

*Last updated: 2026-06-21*

> **Behavioral contract.** RFC 2119 keywords (MUST / SHOULD / MAY). Changes only when the *contract* changes, not when implementation moves.

## Purpose

Provider-agnostic authorization primitives for self-hosted APIs: a claim-keyed **principal allowlist** + an API-wide **default-deny** fallback. One wiring block locks down a single-admin (or small-allowlist) service, independent of the authentication provider.

## Behavior

### Principal allowlist

- `AddPrincipalAllowlist` **MUST** bind `AllowlistOptions` and register `AllowlistAuthorizationHandler` as an `IAuthorizationHandler`.
- The handler **MUST** succeed when `Allowed` is empty (OPEN — any authenticated principal passes; single-admin default).
- When `Allowed` is non-empty, the handler **MUST** succeed only if the principal carries a `ClaimType` claim whose value is in `Allowed`, and **MUST NOT** succeed otherwise.
- The handler **MUST** respect `CaseInsensitive`: `true` ⇒ `OrdinalIgnoreCase`; `false` ⇒ ordinal.
- The handler **MUST NOT** assert authentication itself — that is the composing policy's `RequireAuthenticatedUser`.
- The allowlist **SHOULD** key on the normalized username claim (`wt:username`), independent of the provider's raw claim shape.

### Default-deny

- `AddDefaultDenyAuthorization` **MUST** build the policy with `RequireAuthenticatedUser()` bound to the single named `scheme` passed in.
- When `withAllowlist` is `true`, the policy **MUST** also add `AllowlistRequirement`.
- It **MUST** register the policy under the public `PolicyName` constant **and** set it as the authorization `FallbackPolicy`.
- Endpoints carrying neither `[Authorize]` nor `[AllowAnonymous]` **MUST** be denied by default.
- Anonymous endpoints (e.g. `/health`) **MUST** opt out via `[AllowAnonymous]` / `.AllowAnonymous()`.
- The single named scheme **SHOULD** be the application cookie scheme, so an unauthenticated request yields a clean **401**, not an external-IdP **302**.

### Composition

- `withAllowlist: true` **MUST** require the principal to be both authenticated and (allowlist-)authorized.
- An empty allowlist under `withAllowlist: true` **MUST** reduce to authenticated-only — never deny a valid authenticated principal.

## Non-goals

- Does **not** define roles, scopes, or permissions — that is `Identity/Policies` (`IRolePolicy`).
- Does **not** authenticate, issue cookies, or normalize claims — those are `Identity/Cookies`, the provider packages, and `Identity/Claims`.
- Does **not** register authorization middleware (`UseAuthorization`) — the host owns the pipeline.

## See also

- Spec file: `Authorization.spec.md`
- Folder doc: `authorization.md`
- ASP.NET Core authorization: <https://learn.microsoft.com/aspnet/core/security/authorization/introduction>
