namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Outcome of <see cref="IOtpService.VerifyAsync"/>.</summary>
/// <param name="Success">Whether the code matched and was consumed.</param>
/// <param name="FailureReason">Why verification failed (present on failure).</param>
public sealed record OtpVerificationResult(bool Success, OtpFailureReason? FailureReason)
{
    /// <summary>Successful verification.</summary>
    public static OtpVerificationResult Succeeded() => new(true, null);

    /// <summary>Failed verification with a <paramref name="reason"/>.</summary>
    /// <param name="reason">The failure reason.</param>
    public static OtpVerificationResult Failed(OtpFailureReason reason) => new(false, reason);
}
