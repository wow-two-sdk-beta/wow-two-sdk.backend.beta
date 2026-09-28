namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.EmailSignIn;

/// <summary>Holds how passwordless email sign-in behaves: link and code lifetimes, and email confirmation on use.</summary>
/// <remarks>Set in code with <c>.AddEmailSignIn(o => …)</c> or in the host section <c>Identity:EmailSignIn</c>, which is applied last.</remarks>
public sealed record EmailSignInOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Identity:EmailSignIn";

    /// <summary>Gets or sets how long a sign-in link works. Default 15 minutes.</summary>
    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets or sets how long a sign-in code works. Default 10 minutes.</summary>
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the digits of a sign-in code (4–12). Default 6.</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Gets or sets whether a used link or code confirms the address, which it proves. Default true.</summary>
    public bool ConfirmEmail { get; set; } = true;
}
