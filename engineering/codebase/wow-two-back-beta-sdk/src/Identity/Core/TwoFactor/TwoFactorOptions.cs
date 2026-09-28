namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>Holds the two-factor settings: authenticator label, recovery codes and the delivered-code methods.</summary>
/// <remarks>Set in code with <c>.AddTwoFactor(o => …)</c> or in the host section <c>Identity:TwoFactor</c>, which is applied last.</remarks>
public sealed record TwoFactorOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Identity:TwoFactor";

    /// <summary>Gets or sets the issuer shown in authenticator apps. Default <c>App</c>.</summary>
    public string Issuer { get; set; } = "App";

    /// <summary>Gets or sets the recovery codes issued per generation. Default 10.</summary>
    public int RecoveryCodeCount { get; set; } = 10;

    /// <summary>Gets or sets whether a password sign-in that needs the second factor sends the preferred method's code. Default true.</summary>
    public bool AutoSendCode { get; set; } = true;

    /// <summary>Gets the delivered-code methods by the name a user picks, such as <c>sms</c> or <c>email</c>.</summary>
    public Dictionary<string, TwoFactorMethodOptions> Methods { get; } = new(StringComparer.OrdinalIgnoreCase);
}
