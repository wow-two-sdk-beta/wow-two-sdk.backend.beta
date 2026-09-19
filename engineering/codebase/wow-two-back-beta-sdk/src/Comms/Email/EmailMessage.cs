namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>One outgoing message. Provide <see cref="TextBody"/>, <see cref="HtmlBody"/>, or both.</summary>
public sealed record EmailMessage
{
    /// <summary>Primary recipients.</summary>
    public required IReadOnlyList<EmailAddress> To { get; init; }

    /// <summary>Subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Plain-text body.</summary>
    public string? TextBody { get; init; }

    /// <summary>HTML body.</summary>
    public string? HtmlBody { get; init; }

    /// <summary>Sender; falls back to <see cref="EmailOptions.DefaultFrom"/> when null.</summary>
    public EmailAddress? From { get; init; }

    /// <summary>Reply-to; falls back to <see cref="EmailOptions.DefaultReplyTo"/> when null.</summary>
    public EmailAddress? ReplyTo { get; init; }

    /// <summary>Carbon-copy recipients.</summary>
    public IReadOnlyList<EmailAddress>? Cc { get; init; }

    /// <summary>Blind-carbon-copy recipients.</summary>
    public IReadOnlyList<EmailAddress>? Bcc { get; init; }

    /// <summary>File attachments (provider support varies — SES simple-send rejects them).</summary>
    public IReadOnlyList<EmailAttachment>? Attachments { get; init; }

    /// <summary>Convenience factory for the single-recipient case.</summary>
    /// <param name="to">Recipient address.</param>
    /// <param name="subject">Subject line.</param>
    /// <param name="textBody">Plain-text body.</param>
    /// <param name="htmlBody">Optional HTML body.</param>
    public static EmailMessage Create(string to, string subject, string? textBody = null, string? htmlBody = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return new EmailMessage { To = [new EmailAddress { Address = to }], Subject = subject, TextBody = textBody, HtmlBody = htmlBody };
    }
}
