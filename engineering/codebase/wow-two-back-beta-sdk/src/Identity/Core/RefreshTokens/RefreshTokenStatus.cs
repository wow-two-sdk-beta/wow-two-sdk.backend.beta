namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;

/// <summary>The outcomes of a refresh-token redemption.</summary>
public enum RefreshTokenStatus
{
    /// <summary>Malformed, unknown, revoked, or voided by a stamp rotation or a deleted user.</summary>
    Invalid,

    /// <summary>Redeemed; the result carries the user and the replacement token.</summary>
    Succeeded,

    /// <summary>Past its lifetime.</summary>
    Expired,

    /// <summary>Already consumed; the whole family is now revoked.</summary>
    Reused,
}
