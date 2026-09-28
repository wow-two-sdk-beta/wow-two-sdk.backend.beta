namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Meta;

/// <summary>Holds the WhatsApp Cloud API credentials and sender.</summary>
public sealed record MetaWhatsAppOptions
{
    /// <summary>Gets or sets the system-user access token with <c>whatsapp_business_messaging</c>.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender's phone-number id (not the number itself).</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Graph API version. Default <c>v23.0</c>.</summary>
    public string ApiVersion { get; set; } = "v23.0";

    /// <summary>Gets or sets the API base address. Default <c>https://graph.facebook.com/</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://graph.facebook.com/");
}
