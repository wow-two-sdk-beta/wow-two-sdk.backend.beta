using OtpNet;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp;

/// <summary>Issues and verifies time-based one-time codes against the parameters in <see cref="TotpOptions"/>.</summary>
public sealed class TotpService : ITotpService
{
    private readonly TotpOptions _options;

    /// <summary>Initializes the service with the TOTP parameters it issues and verifies against.</summary>
    /// <param name="options">The step, digit count and verification window.</param>
    public TotpService(TotpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public byte[] GenerateSecret() => KeyGeneration.GenerateRandomKey(_options.SecretBytes);

    /// <inheritdoc />
    public string ToBase32(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return Base32Encoding.ToString(secret);
    }

    /// <inheritdoc />
    public Uri BuildOtpAuthUri(string issuer, string accountName, byte[] secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentNullException.ThrowIfNull(secret);

        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var query = $"secret={ToBase32(secret)}&issuer={Uri.EscapeDataString(issuer)}" +
                    $"&digits={_options.Digits}&period={_options.StepSeconds}";

        return new Uri($"otpauth://totp/{label}?{query}");
    }

    /// <inheritdoc />
    public string ComputeCode(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return Totp(secret).ComputeTotp();
    }

    /// <inheritdoc />
    public bool VerifyCode(byte[] secret, string code)
    {
        ArgumentNullException.ThrowIfNull(secret);

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var window = new VerificationWindow(previous: _options.VerificationSteps, future: _options.VerificationSteps);
        return Totp(secret).VerifyTotp(code, out _, window);
    }

    private OtpNet.Totp Totp(byte[] secret) => new(secret, step: _options.StepSeconds, totpSize: _options.Digits);
}
