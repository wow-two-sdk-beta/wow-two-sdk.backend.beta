namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Outcome of <see cref="IOtpService.CreateAsync"/>.</summary>
/// <param name="Success">Whether a code was created.</param>
/// <param name="Code">The generated code (present on success) — pass it to a delivery handler.</param>
/// <param name="FailureReason">Why creation failed (present on failure).</param>
public sealed record OtpCreationResult(bool Success, string? Code, OtpFailureReason? FailureReason)
{
    /// <summary>Successful creation carrying the generated <paramref name="code"/>.</summary>
    /// <param name="code">The generated code.</param>
    public static OtpCreationResult Succeeded(string code) => new(true, code, null);

    /// <summary>Failed creation with a <paramref name="reason"/>.</summary>
    /// <param name="reason">The failure reason.</param>
    public static OtpCreationResult Failed(OtpFailureReason reason) => new(false, null, reason);
}
