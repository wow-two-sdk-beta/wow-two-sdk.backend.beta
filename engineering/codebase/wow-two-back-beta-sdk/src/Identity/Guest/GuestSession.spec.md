# Guest capability contract

- A guest cookie is an authenticated, expiring bearer capability, protected with ASP.NET Core Data Protection.
- The protection purpose includes protocol version and cookie name. Hosts sharing guest identity must explicitly share their Data Protection application name and key ring.
- Raw GUIDs, invalid protection, expired tokens and empty IDs resolve as anonymous. There is no legacy fallback.
- Registered claims take precedence over guest identity. Guest tokens never authenticate a registered account.
- Provision and clear take effect immediately within the request; clearing prevents reuse of the inbound cookie.
- Expiration uses the registered TimeProvider. Browser MaxAge and protected expiry use the same lifetime.
- Clear removes the browser capability; it does not revoke a stolen copy server-side. Revocation requires a separate session store.
- Persistent deployments must configure durable Data Protection keys; loss of keys loses guest access.
