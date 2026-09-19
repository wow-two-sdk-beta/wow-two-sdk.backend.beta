namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Represents outcome of <see cref="IOtpService.VerifyAsync"/>.</summary>
public sealed record OtpVerificationResult
{
    /// <summary>Whether the code matched and was consumed.</summary>
    public required bool Success { get; init; }

    /// <summary>Why verification failed (present on failure).</summary>
    public required OtpFailureReason? FailureReason { get; init; }

    /// <summary>Successful verification.</summary>
    public static OtpVerificationResult Succeeded() => new() { Success = true, FailureReason = null };

    /// <summary>Failed verification with a <paramref name="reason"/>.</summary>
    /// <param name="reason">The failure reason.</param>
    public static OtpVerificationResult Failed(OtpFailureReason reason) => new() { Success = false, FailureReason = reason };
}
