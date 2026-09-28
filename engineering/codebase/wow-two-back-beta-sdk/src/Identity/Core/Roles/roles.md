# Identity.Core.Roles

Roles slice: role administration (`RoleService`), membership by role name (`UserRoleService`) and role claims.
Principals built by `UserClaimsPrincipalFactory` carry role names and role claims once this slice is registered.

## Quick start

```csharp
identity.AddEntityFrameworkStores<AppDbContext>()
        .AddRoles<IdentityRole>();                        // after the stores: registers the EF role repositories

await roles.CreateAsync(new IdentityRole { Name = "Admin" });
await roles.AddClaimAsync(admin, new Claim("permission", "users.write"));
await memberships.AddToRoleAsync(user, "admin");          // names are case-insensitive
```

## Notes

- `AddRoles<TRole>()` is a builder method: TRole is the role type mapped by `ApplyIdentitySchema`.
- Removing a membership rotates the security stamp so principals holding the role end; an added role appears at next sign-in.
- Deleting a role removes its claims and memberships in the same save; the schema carries no foreign keys.
- Authorization stays with ASP.NET policies or `Identity/Policies` (`AddRolePolicy`) over the role claims.
