namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Telegram;

/// <summary>Message shaping for Telegram OTP delivery.</summary>
public sealed class TelegramOtpOptions
{
    /// <summary>
    /// Message template: <c>{0}</c> = scope display name, <c>{1}</c> = code, <c>{2}</c> = lifetime minutes.
    /// </summary>
    public string MessageTemplate { get; set; } =
        "🔐 {0} login\n\nYour code: {1}\nExpires in {2} minutes.\n\nDo not share this code with anyone.";

    /// <summary>Scope key → human-readable name. Unmapped scopes fall back to the raw key.</summary>
    public IDictionary<string, string> ScopeDisplayNames { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
