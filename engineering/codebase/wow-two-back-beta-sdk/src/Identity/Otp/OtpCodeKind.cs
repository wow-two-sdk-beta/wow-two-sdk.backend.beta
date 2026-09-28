namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Refers to the character set a one-time code draws from.</summary>
public enum OtpCodeKind
{
    /// <summary>Digits <c>0–9</c>; the only kind a phone keypad and SMS autofill handle well.</summary>
    Numeric,

    /// <summary>Upper-case letters and digits without the look-alikes <c>0 O 1 I</c>.</summary>
    Alphanumeric,

    /// <summary>Upper-case letters without the look-alikes <c>I O</c>.</summary>
    Letters,
}
