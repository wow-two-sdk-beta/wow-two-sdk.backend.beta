namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.SendGrid;

/// <summary>SendGrid API settings.</summary>
public sealed record SendGridEmailOptions
{
    /// <summary>SendGrid API key. Required — source from a secret store.</summary>
    public string ApiKey { get; set; } = "";
}
