# API key contract

- A secret is the product's marker plus `RandomLength` (≥ 24, default 32) characters from `[A-Za-z0-9]`, drawn from
  the cryptographic random generator. Only its lowercase hex SHA-256 and a display prefix are stored.
- A fast hash is correct here: secrets are long and random, so there is nothing to brute-force from the hash.
- The scheme reads `Authorization: Bearer {secret}` only when the token starts with the marker; any other Bearer
  token returns no result, so a JWT scheme on the same host reads it.
- The key header (`X-Api-Key` by default) is read as a key whatever its content.
- A presented value of the wrong shape fails without a store lookup; a well-shaped one fails unless the store finds a
  live key. Both failures read the same, so a response never tells a malformed key from a revoked one.
- `TouchAsync` runs at most once per `TouchInterval` per key, judged by the record's `LastUsedAt`.
- The scheme carries each distinct, non-blank scope of the record as a `scope` claim on the key's identity.
- `HasApiKeyScope` reads only the identity the `ApiKey` scheme authenticated; a scope claim from any other scheme
  never counts, so a merged principal cannot borrow one.
- `RequireApiKeyScope` authenticates its policy with the `ApiKey` scheme: no key is 401, a key without the scope 403.
- The gate guards `GuardedPaths` minus `OpenPaths`. With a key it authenticates the `ApiKey` scheme and sets
  `HttpContext.User`; without one it passes loopback callers (when `AllowLocalWithoutKey`) and callers another scheme
  already authenticated, and answers 401 otherwise.
- Local means a loopback or absent remote address. Behind a reverse proxy on the same machine, restore the client
  address with forwarded headers or turn `AllowLocalWithoutKey` off — otherwise every proxied request looks local.
- Key management endpoints belong in `LocalOnlyPaths`: a key reaching one is 403, so a leaked key cannot mint more.
