# Identity.Core.PersonalData

The data-subject rights every account needs (GDPR access, portability, erasure): export what the SDK and the product
hold for a user, and delete the account for good. Products plug in their own sections and erasure steps.

## Quick start

```csharp
identity.AddEntityFrameworkStores<AppDbContext>().AddRoles<AppRole>().AddPersonalData();
builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersonalDataExporter<AppUser>, OrdersExporter>());
builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IAccountDeletionHandler<AppUser>, OrdersEraser>());
```

| Route | Does |
|---|---|
| `GET manage/personal-data` | downloads `personal-data.json` |
| `POST manage/delete-account` | `{ password }` when the account has one; `204`, then the cookie is cleared |

## Notes

- Built-in sections: `account`, `claims`, `logins`, `passkeys`, `sessions`, and `roles` with the roles slice.
- Password hashes, token hashes, security stamps and passkey keys are never exported.
- Deletion runs the product's handlers first, then removes roles, claims, logins, stored tokens, sessions,
  passkeys and the user row in one transaction. Handlers must tolerate running again after a failure.
- Issued access tokens stay valid until they expire; keep their lifetime short.
