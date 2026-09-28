namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Sms;

/// <summary>Holds message shaping for SMS OTP delivery.</summary>
public sealed record SmsOtpOptions
{
    /// <summary>
    /// Gets or sets the message template: <c>{0}</c> = scope display name, <c>{1}</c> = code, <c>{2}</c> = lifetime minutes.
    /// Providers that moderate templates (Eskiz) accept only the approved wording.
    /// </summary>
    public string MessageTemplate { get; set; } = "{1} is your {0} code. It expires in {2} minutes. Do not share it.";

    /// <summary>Scope key → human-readable name. Unmapped scopes fall back to the raw key.</summary>
    public IDictionary<string, string> ScopeDisplayNames { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
