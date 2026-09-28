namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Holds the inputs that steer one-time code creation, verification and message wording.</summary>
/// <remarks>Set in code with <c>AddOtpService(o => …)</c> or in the host section <c>Identity:Otp</c>, which is applied last.</remarks>
public sealed record OtpOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Identity:Otp";

    /// <summary>Gets or sets the characters in a generated code (4–12). Default 6.</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Gets or sets the characters a code draws from. Default <see cref="OtpCodeKind.Numeric"/>.</summary>
    public OtpCodeKind CodeKind { get; set; } = OtpCodeKind.Numeric;

    /// <summary>Gets or sets how long a code stays valid. Default 5 minutes.</summary>
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the minimum gap between two codes for the same <c>(subject, scope)</c>. Default 5s.</summary>
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the wrong attempts allowed against one code before it locks. Default 5.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Gets the wording of delivered codes, per culture.</summary>
    public OtpMessageOptions Messages { get; } = new();

    /// <summary>The spec a code takes when its caller names none.</summary>
    internal OtpCodeSpec DefaultSpec => new() { Kind = CodeKind, Length = CodeLength, Lifetime = CodeLifetime };
}
