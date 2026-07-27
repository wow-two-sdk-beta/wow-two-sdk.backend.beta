namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>Cross-provider email defaults.</summary>
public sealed class EmailOptions
{
    /// <summary>Sender used when a message doesn't set <see cref="EmailMessage.From"/>.</summary>
    public EmailAddress? DefaultFrom { get; set; }

    /// <summary>Reply-to used when a message doesn't set <see cref="EmailMessage.ReplyTo"/>.</summary>
    public EmailAddress? DefaultReplyTo { get; set; }
}
