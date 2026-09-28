using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Defines behavior that words a one-time code for a purpose and channel in the recipient's culture.</summary>
public interface IOtpMessageFormatter
{
    /// <summary>Formats the message carrying <paramref name="code"/>.</summary>
    /// <param name="purpose">What the code is for, such as <c>two-factor</c> or <c>phone-confirmation</c>.</param>
    /// <param name="channel">The channel it travels by, such as <see cref="OtpChannelNameConstants.Sms"/>.</param>
    /// <param name="code">The code.</param>
    /// <param name="lifetime">How long the code stays valid.</param>
    /// <param name="culture">The recipient's culture; null takes the current UI culture.</param>
    OtpMessageModel Format(string purpose, string channel, string code, TimeSpan lifetime, CultureInfo? culture = null);
}
