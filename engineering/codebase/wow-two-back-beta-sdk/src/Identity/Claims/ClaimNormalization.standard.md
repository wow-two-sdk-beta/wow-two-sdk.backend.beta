# ClaimNormalization — standard

*Last updated: 2026-06-21*

> **Behavioral contract.** RFC 2119 (MUST / SHOULD / MAY). Changes only when the *contract* does, not when implementation moves.

## Purpose

Give downstream code one canonical `wt:*` set independent of the OAuth provider: a per-request `IClaimsTransformation` reads `wt:provider`, selects that provider's profile, and projects raw claims onto `wt:user_id` / `wt:email` / `wt:display_name` / `wt:username` / `wt:avatar`.

## Registration

- **MUST** expose `AddClaimNormalization(this IServiceCollection, Action<ClaimNormalizationOptions>? configure = null)` returning `IServiceCollection`.
- **MUST** be opt-in — nothing added unless called.
- **MUST** register `ClaimNormalizer` as an `IClaimsTransformation` additively (`TryAddEnumerable`), without displacing other registrations.
- Repeated calls **MUST NOT** register the normalizer twice.

## Transformation

- **MUST** be idempotent: if `wt:user_id` is already present, return the principal unchanged.
- **MUST** read the provider from `wt:provider` and **MUST NOT** stamp `wt:provider` itself (the OAuth wrapper's job).
- When `wt:provider` is absent, empty, or has no matching profile, **MUST** return the principal unchanged.
- For each canonical field, **MUST** resolve from the profile's ordered source claim types, taking the **first non-empty** match.
- **MUST** accept both the long OAuth2 `ClaimTypes.*` URI and the short OIDC name (`sub`, `email`, `name`, `preferred_username`, `picture`) where the profile lists both.
- **MUST NOT** fabricate: a field the provider does not supply **MUST** be left unset (no empty claim).
- **MUST NOT** mutate or remove existing claims; only **adds** `wt:*`.

## Field guarantees

- `wt:user_id` — **guaranteed** after normalization for every built-in provider whose row is not "verify"; **provider-dependent** for "verify" rows.
- `wt:email` — **provider-dependent**: present only when returned (typically absent for Twitter/X; absent by default for Reddit).
- `wt:display_name` — **provider-dependent**: absent for providers with no display claim (Discord, Reddit); best-effort for Apple (first-consent only).
- `wt:username` — present **only** for providers whose profile maps a handle (GitHub / GitLab / Twitch / Discord / Twitter / Reddit / Yandex / Microsoft `preferred_username`); **MUST NOT** be populated from a `ClaimTypes.Name` the profile classifies as a display name.
- `wt:avatar` — **provider-dependent**: direct avatar claim when present; else the synthesizer result **only** when `SynthesizeAvatars` is enabled; else absent.

## Options

- All toggles **MUST** live on `ClaimNormalizationOptions` and be init-only.
- `Profiles` **MUST** be pre-seeded with the built-in registry and key schemes case-insensitively.
- A host **MUST** be able to add/override a `(scheme → profile)` mapping via `AddProvider`.
- Avatar synthesis **MUST** be host-toggleable via `SynthesizeAvatars`.
- Defaults **MUST** be safe with no further config (synthesis on, all built-ins registered).

## Failure modes

- A synthesizer **MUST** return `null` rather than throw when it cannot build a URL; a `null` result **MUST** leave `wt:avatar` unset.
- **MUST NOT** throw on a principal lacking expected provider claims — degrades to fewer (or no) `wt:*` claims.

## Non-goals

- Does **not** authenticate, issue, or validate tokens — only reshapes claims already on the principal.
- Does **not** call any provider API or perform network I/O.
- Does **not** define authorization policy or persist a user record — both are the consumer's job.

## See also

- Spec: [`ClaimNormalization.spec.md`](./ClaimNormalization.spec.md)
- Folder lead: [`claims.md`](./claims.md)
- Underlying lib: [AspNet.Security.OAuth.Providers](https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers)
