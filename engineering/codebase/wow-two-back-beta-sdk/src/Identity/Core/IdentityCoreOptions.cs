namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Options for identity core and its slices. Mutable properties so <c>Action&lt;IdentityCoreOptions&gt;</c> composes.</summary>
public sealed record IdentityCoreOptions
{
    /// <summary>User account rules.</summary>
    public UserOptions User { get; } = new();

    /// <summary>Password rules (consumed by the password slice).</summary>
    public PasswordOptions Password { get; } = new();

    /// <summary>Lockout rules (consumed by the lockout slice).</summary>
    public LockoutOptions Lockout { get; } = new();
}

/// <summary>User account rules.</summary>
public sealed record UserOptions
{
    /// <summary>Require each account to have a unique email. Default true.</summary>
    public bool RequireUniqueEmail { get; set; } = true;
}

/// <summary>Password rules (consumed by the password slice).</summary>
public sealed record PasswordOptions
{
    /// <summary>Minimum password length. Default 8.</summary>
    public int MinLength { get; set; } = 8;
}

/// <summary>Lockout rules (consumed by the lockout slice).</summary>
public sealed record LockoutOptions
{
    /// <summary>Failed attempts before lockout. Default 5.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>How long an account stays locked out. Default 5 minutes.</summary>
    public TimeSpan DefaultLockout { get; set; } = TimeSpan.FromMinutes(5);
}
