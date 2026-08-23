using OtpNet;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp;

/// <summary>Defines the contract for issuing and verifying time-based one-time codes (RFC 6238).</summary>
public interface ITotpService
{
    /// <summary>Generates a random shared secret of the configured length.</summary>
    /// <returns>The raw secret bytes.</returns>
    byte[] GenerateSecret();

    /// <summary>Encodes a secret as base32 for the standard <c>otpauth://</c> URI.</summary>
    /// <param name="secret">The raw secret to encode.</param>
    /// <returns>The base32 form.</returns>
    string ToBase32(byte[] secret);

    /// <summary>Builds a standard <c>otpauth://totp/…</c> URI suitable for a QR code.</summary>
    /// <param name="issuer">Issuer label shown in the authenticator app.</param>
    /// <param name="accountName">Account label shown in the authenticator app.</param>
    /// <param name="secret">The shared secret to embed.</param>
    /// <returns>The URI carrying the configured digits and period.</returns>
    Uri BuildOtpAuthUri(string issuer, string accountName, byte[] secret);

    /// <summary>Computes the code current for <paramref name="secret"/>.</summary>
    /// <param name="secret">The shared secret to compute from.</param>
    /// <returns>The code, at the configured digit length.</returns>
    string ComputeCode(byte[] secret);

    /// <summary>Verifies a code against <paramref name="secret"/> within the configured window.</summary>
    /// <param name="secret">The shared secret to verify against.</param>
    /// <param name="code">The code the user entered.</param>
    /// <returns><see langword="true"/> when the code matches a step inside the window.</returns>
    bool VerifyCode(byte[] secret, string code);
}

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
