namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Represents the shape of one kind of code: its characters, length and lifetime.</summary>
/// <remarks>Binds from configuration, so a host sets it per purpose, such as <c>Identity:TwoFactor:Methods:email:Code</c>.</remarks>
public sealed record OtpCodeSpec
{
    /// <summary>The characters the code draws from. Default <see cref="OtpCodeKind.Numeric"/>.</summary>
    public OtpCodeKind Kind { get; set; } = OtpCodeKind.Numeric;

    /// <summary>Characters in the code (4–12). Default 6.</summary>
    public int Length { get; set; } = 6;

    /// <summary>How long the code stays valid; null takes <see cref="OtpOptions.CodeLifetime"/>.</summary>
    public TimeSpan? Lifetime { get; set; }

    /// <summary>Whether the spec describes a code the generator can produce.</summary>
    internal bool IsValid => Length is >= 4 and <= 12 && Enum.IsDefined(Kind) && (Lifetime is null || Lifetime > TimeSpan.Zero);
}
