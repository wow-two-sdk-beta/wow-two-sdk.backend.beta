namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>Represents what the provider said about a token.</summary>
public sealed record CaptchaVerifyResult
{
    /// <summary>Gets whether the provider accepted the token.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets whether the provider could not be reached or answered unreadably.</summary>
    public bool Unavailable { get; init; }

    /// <summary>Gets the provider's error codes, such as <c>timeout-or-duplicate</c>.</summary>
    public IReadOnlyList<string> ErrorCodes { get; init; } = [];

    /// <summary>Gets the hostname the challenge was solved on.</summary>
    public string? Hostname { get; init; }

    /// <summary>Gets the action the widget named.</summary>
    public string? Action { get; init; }

    /// <summary>Gets the score, when the provider scores (1.0 is very likely human).</summary>
    public double? Score { get; init; }

    /// <summary>Gets when the challenge was solved.</summary>
    public DateTimeOffset? ChallengedAt { get; init; }
}
