namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Defines behavior that generates one-time codes to an <see cref="OtpCodeSpec"/>; the default is <see cref="OtpCodeGenerator"/>.</summary>
public interface IOtpCodeGenerator
{
    /// <summary>Generates one fresh code.</summary>
    /// <param name="spec">The characters and length of the code.</param>
    string Generate(OtpCodeSpec spec);
}
