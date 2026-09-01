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
