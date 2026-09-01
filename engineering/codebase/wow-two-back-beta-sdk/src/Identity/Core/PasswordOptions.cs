namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Password rules (consumed by the password slice).</summary>
public sealed record PasswordOptions
{
    /// <summary>Minimum password length. Default 8.</summary>
    public int MinLength { get; set; } = 8;
}
