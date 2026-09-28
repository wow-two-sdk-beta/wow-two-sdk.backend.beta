namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.WhatsApp;

/// <summary>Holds the broker choice and the authentication template WhatsApp OTP delivery sends.</summary>
/// <remarks>
/// WhatsApp delivers codes only through an approved authentication template (<c>{{1}}</c> = the code, plus a copy-code
/// button); without <see cref="TemplateName"/> the handler sends free text, which reaches only recipients who wrote
/// to the business within 24 hours.
/// </remarks>
public sealed record WhatsAppOtpOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Identity:Otp:WhatsApp";

    /// <summary>Gets or sets the WhatsApp broker codes go through, such as <c>meta</c>; null takes the default broker.</summary>
    public string? Broker { get; set; }

    /// <summary>Gets or sets the approved authentication template name (Meta) or content SID (Twilio).</summary>
    public string? TemplateName { get; set; }

    /// <summary>Gets or sets the template language when the recipient's culture has none. Default <c>en_US</c>.</summary>
    public string TemplateLanguage { get; set; } = "en_US";

    /// <summary>Gets the template language per culture, such as <c>ru</c> → <c>ru</c>; parents apply (<c>uz-Latn</c> → <c>uz</c>).</summary>
    public Dictionary<string, string> TemplateLanguages { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets whether the template has a copy-code button that takes the code. Default true.</summary>
    public bool CopyCodeButton { get; set; } = true;
}
