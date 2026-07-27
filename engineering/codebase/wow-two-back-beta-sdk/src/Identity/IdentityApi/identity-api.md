# WoW.Two.Sdk.Backend.Beta.Identity.IdentityApi

> ASP.NET Core Identity API endpoints (`MapIdentityApi<TUser>`) preset.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Identity.IdentityApi
```

## Usage

```csharp
public class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext(options) { }

builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(...));
builder.Services.AddIdentityApiEndpoints<AppDb>();

var app = builder.Build();
app.MapGroup("/auth").MapIdentityApi<IdentityUser>();
```

Endpoints provided: `/register`, `/login`, `/refresh`, `/confirmEmail`, `/forgotPassword`, `/resetPassword`, `/manage/info`, `/manage/2fa`.

Defaults applied:
- Unique email required
- Password: min 12 chars, digit, upper, lower (no special)
- Email confirmation required for sign-in
- Lockout after 5 failed attempts for 15 min
