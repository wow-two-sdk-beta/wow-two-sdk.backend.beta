namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Eskiz;

/// <summary>Holds Eskiz (Uzbekistan) account credentials and the sender nickname.</summary>
public sealed record EskizSmsOptions
{
    /// <summary>Account email used to obtain the bearer token.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Account password (the API secret from the Eskiz cabinet).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Sender nickname when neither the message nor <see cref="SmsOptions.DefaultFrom"/> names one. Default <c>4546</c>.</summary>
    public string From { get; set; } = "4546";

    /// <summary>Optional delivery-report callback URL.</summary>
    public Uri? CallbackUrl { get; set; }

    /// <summary>API base address. Default <c>https://notify.eskiz.uz/api/</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://notify.eskiz.uz/api/");
}
