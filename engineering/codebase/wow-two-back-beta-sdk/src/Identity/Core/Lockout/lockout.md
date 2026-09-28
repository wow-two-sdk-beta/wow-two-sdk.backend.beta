# Identity.Core.Lockout

Lockout slice: count failed attempts on the user row and lock for a window once the maximum is reached.

## Quick start

```csharp
services.AddUserAccounts<AppUser>(o => { o.Lockout.MaxFailedAttempts = 5; o.Lockout.DefaultLockout = TimeSpan.FromMinutes(15); })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddLockout();

if (lockout.IsLockedOut(user)) return Locked();
if (!await passwords.CheckPasswordAsync(user, input)) { await lockout.RecordFailedAttemptAsync(user); return Denied(); }
await lockout.ResetFailedAttemptsAsync(user);
```

## Notes

- The attempt that reaches the maximum sets `LockoutEnd = now + DefaultLockout` and resets the count.
- Users created while `EnabledForNewUsers` is false are never counted or locked; `SetLockoutEnabledAsync` changes it.
- `LockUntilAsync(user, end)` locks or unlocks explicitly but keeps sessions; rotate the stamp to end them.
- Automatic lockout never rotates the stamp, so an attacker guessing passwords cannot sign the owner out.
