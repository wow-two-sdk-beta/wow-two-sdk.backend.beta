namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Represents outcome of <see cref="IOtpService.CreateAsync"/>.</summary>
public sealed record OtpCreationResult
{
    /// <summary>Whether a code was created.</summary>
    public required bool Success { get; init; }

    /// <summary>The generated code (present on success) — pass it to a delivery handler.</summary>
    public required string? Code { get; init; }

    /// <summary>Why creation failed (present on failure).</summary>
    public required OtpFailureReason? FailureReason { get; init; }

    /// <summary>Successful creation carrying the generated <paramref name="code"/>.</summary>
    /// <param name="code">The generated code.</param>
    public static OtpCreationResult Succeeded(string code) => new() { Success = true, Code = code, FailureReason = null };

    /// <summary>Failed creation with a <paramref name="reason"/>.</summary>
    /// <param name="reason">The failure reason.</param>
    public static OtpCreationResult Failed(OtpFailureReason reason) => new() { Success = false, Code = null, FailureReason = reason };
}
