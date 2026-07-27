# Identity.Policies

Role-in-scope gating: `IRolePolicy.IsAllowed(role, scope)` with a dictionary-backed default.
Replaces hard-coded `AllowedRoles` dicts in controllers.

```csharp
builder.Services.AddRolePolicy(o =>
{
    o.Map["admin"]    = new HashSet<string> { "admin" };
    o.Map["crm"]      = new HashSet<string> { "agent", "admin" };
    o.Map["channels"] = new HashSet<string> { "agent", "admin" };
});

// in a handler/controller
if (!_policy.IsAllowed(user.Role, body.Service)) return Forbid();
```

- Unknown scope → deny. Strings are consumer vocabulary — the SDK doesn't define roles.
- Swap the impl by registering your own `IRolePolicy` before `AddRolePolicy` (`TryAdd` semantics).

See also: `Identity/Otp` + `Identity/Jwt/Issuance` — the three compose into a passwordless login flow.
