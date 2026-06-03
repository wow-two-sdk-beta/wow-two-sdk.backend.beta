using OtpNet;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp;

/// <summary>
/// TOTP helpers (RFC 6238) — generate secrets, compute codes, verify.
/// </summary>
public static class TotpService
{
    /// <summary>Step in seconds. RFC 6238 default is 30.</summary>
    public const int StepSeconds = 30;

    /// <summary>Code digit count. Most authenticator apps use 6.</summary>
    public const int CodeDigits = 6;

    /// <summary>Generate a 20-byte (160-bit) random secret. Encode for QR via <see cref="ToBase32"/>.</summary>
    public static byte[] GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return key;
    }

    /// <summary>Encode a secret as base32 for the standard `otpauth://` URI.</summary>
    public static string ToBase32(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Base32Encoding.ToString(secret);
    }

    /// <summary>Build a standard `otpauth://totp/...` URI suitable for QR codes.</summary>
    public static Uri BuildOtpAuthUri(string issuer, string accountName, byte[] secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentNullException.ThrowIfNull(secret);

        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var b32 = ToBase32(secret);
        var qs = $"secret={b32}&issuer={Uri.EscapeDataString(issuer)}&digits={CodeDigits}&period={StepSeconds}";
        return new Uri($"otpauth://totp/{label}?{qs}");
    }

    /// <summary>Compute the current TOTP for a secret.</summary>
    public static string ComputeCode(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return new OtpNet.Totp(secret, step: StepSeconds, totpSize: CodeDigits).ComputeTotp();
    }

    /// <summary>
    /// Verify a TOTP code with ±1 step tolerance (90s window centered on now).
    /// </summary>
    public static bool VerifyCode(byte[] secret, string code)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (string.IsNullOrWhiteSpace(code)) return false;

        var totp = new OtpNet.Totp(secret, step: StepSeconds, totpSize: CodeDigits);
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
