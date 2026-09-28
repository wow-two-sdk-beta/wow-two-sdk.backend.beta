namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>Holds the reserved two-factor method names; configured methods take any other name.</summary>
public static class TwoFactorMethodNameConstants
{
    /// <summary>The authenticator app (TOTP); always available once a key is set up.</summary>
    public const string Authenticator = "authenticator";
}
