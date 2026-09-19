namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Holds lockout rules (consumed by the lockout slice).</summary>
public sealed record LockoutOptions
{
    /// <summary>Failed attempts before lockout. Default 5.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>How long an account stays locked out. Default 5 minutes.</summary>
    public TimeSpan DefaultLockout { get; set; } = TimeSpan.FromMinutes(5);
}
