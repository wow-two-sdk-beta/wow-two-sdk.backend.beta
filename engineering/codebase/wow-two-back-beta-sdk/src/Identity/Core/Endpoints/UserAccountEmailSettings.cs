namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>
/// Holds the <c>UserAccounts:Emails</c> configuration section: the links and texts of confirmation and password-reset
/// emails, and of passwordless sign-in links and codes. A missing link template sends no email of that kind; placeholders
/// are <c>{userId}</c>, <c>{email}</c>, <c>{token}</c> (URL-escaped) in links, <c>{link}</c> and <c>{code}</c> in bodies.
/// </summary>
public sealed record UserAccountEmailSettings
{
    /// <summary>The configuration section the settings bind from.</summary>
    public const string SectionName = "UserAccounts:Emails";

    /// <summary>The frontend link confirming an email, e.g. <c>https://app.example/confirm-email?userId={userId}&amp;token={token}</c>.</summary>
    public string? ConfirmationLink { get; set; }

    /// <summary>The frontend link resetting a password, e.g. <c>https://app.example/reset-password?email={email}&amp;token={token}</c>.</summary>
    public string? ResetLink { get; set; }

    /// <summary>The frontend link signing in by email, e.g. <c>https://app.example/magic?email={email}&amp;token={token}</c>.</summary>
    public string? MagicLink { get; set; }

    /// <summary>Subject of the sign-in link email.</summary>
    public string MagicLinkSubject { get; set; } = "Your sign-in link";

    /// <summary>Plain-text body of the sign-in link email.</summary>
    public string MagicLinkBody { get; set; } = "Open this link to sign in: {link}\nIt works once and expires soon. If you did not ask for it, ignore this email.";

    /// <summary>Subject of the sign-in code email.</summary>
    public string SignInCodeSubject { get; set; } = "Your sign-in code";

    /// <summary>Plain-text body of the sign-in code email; <c>{code}</c> is the code.</summary>
    public string SignInCodeBody { get; set; } = "Your sign-in code is {code}. It works once and expires soon. If you did not ask for it, ignore this email.";

    /// <summary>Subject of the confirmation email.</summary>
    public string ConfirmationSubject { get; set; } = "Confirm your email";

    /// <summary>Plain-text body of the confirmation email.</summary>
    public string ConfirmationBody { get; set; } = "Open this link to confirm your email: {link}";

    /// <summary>Subject of the password-reset email.</summary>
    public string ResetSubject { get; set; } = "Reset your password";

    /// <summary>Plain-text body of the password-reset email.</summary>
    public string ResetBody { get; set; } = "Open this link to choose a new password: {link}\nIf you did not ask for it, ignore this email.";
}
