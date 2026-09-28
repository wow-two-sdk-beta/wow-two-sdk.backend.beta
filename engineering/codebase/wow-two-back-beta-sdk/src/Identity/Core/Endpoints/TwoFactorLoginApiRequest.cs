namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Completes a sign-in whose first factor answered with a two-factor ticket, such as an emailed link.</summary>
public sealed record TwoFactorLoginApiRequest
{
    /// <summary>The <c>userId</c> from the 401 challenge.</summary>
    public required string UserId { get; init; }

    /// <summary>The <c>twoFactorTicket</c> from the 401 challenge.</summary>
    public required string Ticket { get; init; }

    /// <summary>The second-factor code; without it and a recovery code, the challenge repeats and sends a delivered code.</summary>
    public string? TwoFactorCode { get; init; }

    /// <summary>The method, such as <c>sms</c>; null takes the preferred method.</summary>
    public string? TwoFactorMethod { get; init; }

    /// <summary>A recovery code, instead of a second-factor code.</summary>
    public string? RecoveryCode { get; init; }
}
