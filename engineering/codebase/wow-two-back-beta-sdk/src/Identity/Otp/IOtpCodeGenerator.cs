namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Defines the OTP code-generation strategy; defaults to <see cref="NumericOtpCodeGenerator"/>, register your own for alphanumeric or custom formats.</summary>
public interface IOtpCodeGenerator
{
    /// <summary>Generates one fresh code.</summary>
    string Generate();
}
