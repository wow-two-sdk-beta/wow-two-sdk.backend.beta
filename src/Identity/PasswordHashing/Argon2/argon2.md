# WoW.Two.Sdk.Backend.Beta.Identity.PasswordHashing.Argon2

> Argon2id password hasher — drops into ASP.NET Core Identity as `IPasswordHasher<TUser>`. Replaces PBKDF2.

OWASP 2024 baseline parameters: m=19MiB, t=4, p=1.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.PasswordHashing.Argon2
```

## Usage

```csharp
builder.Services.AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<AppDb>();
builder.Services.UseArgon2PasswordHasher<IdentityUser>();   // must run AFTER AddIdentityCore
```

## Hash format

```
$argon2id$v=19$m=19456,t=4,p=1$<base64-salt>$<base64-hash>
```

## See also

- [Konscious.Security.Cryptography](https://github.com/kmaragon/Konscious.Security.Cryptography)
- [OWASP password storage cheat sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
