using System.Security.Cryptography;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Generates cryptographically random codes of every <see cref="OtpCodeKind"/>; numeric codes keep leading zeros.</summary>
public sealed class OtpCodeGenerator : IOtpCodeGenerator
{
    private const string Digits = "0123456789";
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LettersAndDigits = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <inheritdoc />
    public string Generate(OtpCodeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentOutOfRangeException.ThrowIfLessThan(spec.Length, 4, nameof(spec));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(spec.Length, 12, nameof(spec));

        var alphabet = spec.Kind switch
        {
            OtpCodeKind.Numeric => Digits,
            OtpCodeKind.Alphanumeric => LettersAndDigits,
            OtpCodeKind.Letters => Letters,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, "Unknown code kind."),
        };
        return RandomNumberGenerator.GetString(alphabet, spec.Length);
    }
}
