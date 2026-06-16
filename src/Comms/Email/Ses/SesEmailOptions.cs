namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Ses;

/// <summary>Amazon SES (v2 API) settings. Credentials come from the standard AWS chain.</summary>
public sealed class SesEmailOptions
{
    /// <summary>AWS region system name (e.g. <c>us-east-1</c>). Null = region from the AWS default chain.</summary>
    public string? Region { get; set; }
}
