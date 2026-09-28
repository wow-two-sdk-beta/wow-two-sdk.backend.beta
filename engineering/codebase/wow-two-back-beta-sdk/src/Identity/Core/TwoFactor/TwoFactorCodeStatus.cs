namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>Refers to the outcome of sending a two-factor code.</summary>
public enum TwoFactorCodeStatus
{
    /// <summary>The channel accepted the code.</summary>
    Sent,

    /// <summary>The method reads its code on the device (authenticator); nothing is sent.</summary>
    NotDeliverable,

    /// <summary>The method is not configured, its channel is not registered, or the account has no confirmed address.</summary>
    Unavailable,

    /// <summary>A code for the method was sent moments ago.</summary>
    RateLimited,

    /// <summary>The channel refused the code.</summary>
    DeliveryFailed,
}
