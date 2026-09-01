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
