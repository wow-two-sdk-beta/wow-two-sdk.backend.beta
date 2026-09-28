using WoW.Two.Sdk.Backend.Beta.Identity.Otp;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>Holds one delivered two-factor method: the channel, the broker behind it, the address and the code shape.</summary>
/// <remarks>Configured under <c>Identity:TwoFactor:Methods:{name}</c>; the name is what a user picks, such as <c>sms</c>.</remarks>
public sealed record TwoFactorMethodOptions
{
    /// <summary>Gets or sets the delivery channel, such as <see cref="OtpChannelNameConstants.Sms"/>; any registered channel name works.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>Gets or sets the broker of the channel, such as <c>eskiz</c> or <c>meta</c>; null takes the channel's default.</summary>
    public string? Broker { get; set; }

    /// <summary>Gets or sets the account field the code goes to; null derives it from the channel (email → email, telegram → login, else phone).</summary>
    public TwoFactorAddressKind? Address { get; set; }

    /// <summary>Gets or sets the external-login provider for <see cref="TwoFactorAddressKind.ExternalLogin"/>. Default <c>Telegram</c>.</summary>
    public string LoginProvider { get; set; } = "Telegram";

    /// <summary>Gets the code's characters, length and lifetime.</summary>
    public OtpCodeSpec Code { get; } = new();

    /// <summary>The address kind in effect.</summary>
    internal TwoFactorAddressKind AddressKind => Address ?? Channel.ToLowerInvariant() switch
    {
        OtpChannelNameConstants.Email => TwoFactorAddressKind.Email,
        OtpChannelNameConstants.Telegram => TwoFactorAddressKind.ExternalLogin,
        _ => TwoFactorAddressKind.PhoneNumber,
    };
}
