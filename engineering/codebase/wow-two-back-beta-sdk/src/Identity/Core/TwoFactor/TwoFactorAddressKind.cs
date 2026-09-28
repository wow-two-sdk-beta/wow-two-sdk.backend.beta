namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>Refers to the account field a delivered two-factor code is addressed to.</summary>
public enum TwoFactorAddressKind
{
    /// <summary>The confirmed phone number (SMS, WhatsApp, Telegram Gateway).</summary>
    PhoneNumber,

    /// <summary>The confirmed email address.</summary>
    Email,

    /// <summary>The provider key of an external login, such as the Telegram user id a bot writes to.</summary>
    ExternalLogin,
}
