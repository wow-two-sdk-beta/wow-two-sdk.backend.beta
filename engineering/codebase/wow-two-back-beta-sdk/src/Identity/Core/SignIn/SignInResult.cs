using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.SignIn;

/// <summary>Carries a sign-in attempt's status, and the principal or two-factor ticket it produced.</summary>
public sealed record SignInResult
{
    /// <summary>The outcome.</summary>
    public required SignInStatus Status { get; init; }

    /// <summary>The principal to issue as a cookie or token; set only when <see cref="Status"/> is <see cref="SignInStatus.Succeeded"/>.</summary>
    public ClaimsPrincipal? Principal { get; init; }

    /// <summary>The user id; set from <see cref="SignInStatus.NotAllowed"/> onwards, never for a failed or locked attempt.</summary>
    public string? UserId { get; init; }

    /// <summary>A short-lived ticket to present with the second factor; set for <see cref="SignInStatus.RequiresTwoFactor"/> when purpose tokens are registered.</summary>
    public string? TwoFactorTicket { get; init; }

    /// <summary>Whether the user is signed in.</summary>
    public bool Succeeded => Status == SignInStatus.Succeeded;
}
