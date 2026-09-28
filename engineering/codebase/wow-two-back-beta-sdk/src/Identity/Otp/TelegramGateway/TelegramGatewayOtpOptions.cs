namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.TelegramGateway;

/// <summary>Holds the Telegram Gateway API token and sender for phone-number code delivery.</summary>
/// <remarks>The Gateway delivers only numeric codes of 4–8 digits and keeps them valid for 30–3600 seconds.</remarks>
public sealed record TelegramGatewayOtpOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Identity:Otp:TelegramGateway";

    /// <summary>Gets or sets the Gateway API access token.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the verified channel the code is sent from; null sends from Telegram's verification chat.</summary>
    public string? SenderUsername { get; set; }

    /// <summary>Gets or sets the URL Telegram reports delivery status to.</summary>
    public string? CallbackUrl { get; set; }

    /// <summary>Gets or sets the API base address. Default <c>https://gatewayapi.telegram.org/</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://gatewayapi.telegram.org/");
}
