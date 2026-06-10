namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>
/// Code-generation strategy. Default is cryptographically random numeric digits
/// (<see cref="NumericOtpCodeGenerator"/>); register your own for alphanumeric or custom formats.
/// </summary>
public interface IOtpCodeGenerator
{
    /// <summary>Generates one fresh code.</summary>
    string Generate();
}
