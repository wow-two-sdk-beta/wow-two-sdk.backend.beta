namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;

/// <summary>Represents one outgoing WhatsApp message; set <see cref="Template"/> or <see cref="Text"/>.</summary>
/// <remarks>WhatsApp accepts free text only within 24 hours of the recipient's last message; codes use an approved template.</remarks>
public sealed record WhatsAppMessage
{
    /// <summary>Recipient in E.164 (<c>+998901234567</c>); brokers reformat it for their provider.</summary>
    public required string To { get; init; }

    /// <summary>Free text; ignored when <see cref="Template"/> is set.</summary>
    public string? Text { get; init; }

    /// <summary>An approved template and its values.</summary>
    public WhatsAppTemplate? Template { get; init; }
}
