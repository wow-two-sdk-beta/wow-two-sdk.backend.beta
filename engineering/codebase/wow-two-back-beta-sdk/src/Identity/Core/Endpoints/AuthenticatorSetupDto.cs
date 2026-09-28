namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>A new authenticator key to show as text or a QR code; it takes effect once a code verifies it.</summary>
public sealed record AuthenticatorSetupDto
{
    /// <summary>The base32 key for manual entry.</summary>
    public required string SharedKey { get; init; }

    /// <summary>The <c>otpauth://</c> URI to encode as a QR code.</summary>
    public required string AuthenticatorUri { get; init; }
}
