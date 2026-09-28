namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Holds the preconditions the sign-in slice enforces.</summary>
public sealed record SignInOptions
{
    /// <summary>Refuse sign-in until the email is confirmed. Default false.</summary>
    public bool RequireConfirmedEmail { get; set; }

    /// <summary>Refuse sign-in until the phone number is confirmed. Default false.</summary>
    public bool RequireConfirmedPhoneNumber { get; set; }

    /// <summary>Accept an email address where a user name is expected. Default true.</summary>
    public bool AllowEmailLogin { get; set; } = true;
}
