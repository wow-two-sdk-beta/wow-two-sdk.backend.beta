namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Holds user account rules.</summary>
public sealed record UserOptions
{
    /// <summary>Require each account to have a unique email. Default true.</summary>
    public bool RequireUniqueEmail { get; set; } = true;
}
