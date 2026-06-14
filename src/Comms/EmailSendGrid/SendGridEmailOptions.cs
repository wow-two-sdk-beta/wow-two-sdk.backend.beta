namespace WoW.Two.Sdk.Backend.Beta.Comms.EmailSendGrid;

/// <summary>SendGrid API settings.</summary>
public sealed class SendGridEmailOptions
{
    /// <summary>SendGrid API key. Required — source from a secret store.</summary>
    public string ApiKey { get; set; } = "";
}
