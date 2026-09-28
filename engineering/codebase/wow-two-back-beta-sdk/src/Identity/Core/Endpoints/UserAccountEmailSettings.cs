namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>
/// Holds the <c>UserAccounts:Emails</c> configuration section: the links and texts of confirmation and password-reset
/// emails. A missing link template sends no email of that kind; placeholders are <c>{userId}</c>, <c>{email}</c>,
/// <c>{token}</c> (URL-escaped) in links and <c>{link}</c> in bodies.
/// </summary>
public sealed record UserAccountEmailSettings
{
    /// <summary>The configuration section the settings bind from.</summary>
    public const string SectionName = "UserAccounts:Emails";

    /// <summary>The frontend link confirming an email, e.g. <c>https://app.example/confirm-email?userId={userId}&amp;token={token}</c>.</summary>
    public string? ConfirmationLink { get; set; }

    /// <summary>The frontend link resetting a password, e.g. <c>https://app.example/reset-password?email={email}&amp;token={token}</c>.</summary>
    public string? ResetLink { get; set; }

    /// <summary>Subject of the confirmation email.</summary>
    public string ConfirmationSubject { get; set; } = "Confirm your email";

    /// <summary>Plain-text body of the confirmation email.</summary>
    public string ConfirmationBody { get; set; } = "Open this link to confirm your email: {link}";

    /// <summary>Subject of the password-reset email.</summary>
    public string ResetSubject { get; set; } = "Reset your password";

    /// <summary>Plain-text body of the password-reset email.</summary>
    public string ResetBody { get; set; } = "Open this link to choose a new password: {link}\nIf you did not ask for it, ignore this email.";
}
