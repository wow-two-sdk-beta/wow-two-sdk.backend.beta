namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>Holds the authenticator label and recovery-code count.</summary>
public sealed record TwoFactorOptions
{
    /// <summary>Issuer shown in authenticator apps. Default <c>App</c>.</summary>
    public string Issuer { get; set; } = "App";

    /// <summary>Recovery codes issued per generation. Default 10.</summary>
    public int RecoveryCodeCount { get; set; } = 10;
}
