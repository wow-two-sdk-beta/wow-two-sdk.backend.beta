namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Holds password rules (consumed by the password slice). Defaults follow NIST SP 800-63B: length over composition.</summary>
public sealed record PasswordOptions
{
    /// <summary>Minimum password length. Default 8.</summary>
    public int MinLength { get; set; } = 8;

    /// <summary>Maximum password length; bounds the hashing cost a caller can force. Default 128.</summary>
    public int MaxLength { get; set; } = 128;

    /// <summary>Require an ASCII digit. Default false.</summary>
    public bool RequireDigit { get; set; }

    /// <summary>Require a lowercase letter. Default false.</summary>
    public bool RequireLowercase { get; set; }

    /// <summary>Require an uppercase letter. Default false.</summary>
    public bool RequireUppercase { get; set; }

    /// <summary>Require a character that is neither a letter nor a digit. Default false.</summary>
    public bool RequireNonAlphanumeric { get; set; }

    /// <summary>Minimum number of distinct characters. Default 1.</summary>
    public int RequiredUniqueChars { get; set; } = 1;

    /// <summary>Reject a password equal to the account's user name or email, ignoring case. Default true.</summary>
    public bool RejectAccountIdentifiers { get; set; } = true;
}
