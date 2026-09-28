namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>Represents the outcome of a request's captcha check.</summary>
public sealed record CaptchaResult
{
    /// <summary>Gets whether the request passed.</summary>
    public bool Passed => Failure == CaptchaFailure.None;

    /// <summary>Gets why it failed; <see cref="CaptchaFailure.None"/> when it passed.</summary>
    public required CaptchaFailure Failure { get; init; }

    /// <summary>Gets the provider's answer; null when no token was sent or checks are off.</summary>
    public CaptchaVerifyResult? Verification { get; init; }
}
