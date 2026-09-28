namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Email;

/// <summary>Holds the sender of emailed one-time codes.</summary>
public sealed record EmailOtpOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Identity:Otp:Email";

    /// <summary>Gets or sets the sender address; null leaves the email broker's default.</summary>
    public string? From { get; set; }

    /// <summary>Gets or sets the sender display name.</summary>
    public string? FromName { get; set; }
}
