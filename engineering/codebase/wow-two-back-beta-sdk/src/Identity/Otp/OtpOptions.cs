namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Tunable inputs for OTP creation and verification.</summary>
public sealed class OtpOptions
{
    /// <summary>Digits in a generated code (4–10). Default 6.</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>How long a code stays valid. Default 5 minutes.</summary>
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Minimum gap between two codes for the same <c>(subject, scope)</c>. Default 5s.</summary>
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Wrong attempts allowed against one code before it locks. Default 5.</summary>
    public int MaxAttempts { get; set; } = 5;
}
