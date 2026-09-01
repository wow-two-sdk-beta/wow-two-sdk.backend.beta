namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>One outgoing message. Provide <see cref="TextBody"/>, <see cref="HtmlBody"/>, or both.</summary>
/// <param name="To">Primary recipients.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="TextBody">Plain-text body.</param>
/// <param name="HtmlBody">HTML body.</param>
/// <param name="From">Sender; falls back to <see cref="EmailOptions.DefaultFrom"/> when null.</param>
/// <param name="ReplyTo">Reply-to; falls back to <see cref="EmailOptions.DefaultReplyTo"/> when null.</param>
/// <param name="Cc">Carbon-copy recipients.</param>
/// <param name="Bcc">Blind-carbon-copy recipients.</param>
/// <param name="Attachments">File attachments (provider support varies — SES simple-send rejects them).</param>
public sealed record EmailMessage(
    IReadOnlyList<EmailAddress> To,
    string Subject,
    string? TextBody = null,
    string? HtmlBody = null,
    EmailAddress? From = null,
    EmailAddress? ReplyTo = null,
    IReadOnlyList<EmailAddress>? Cc = null,
    IReadOnlyList<EmailAddress>? Bcc = null,
    IReadOnlyList<EmailAttachment>? Attachments = null)
{
    /// <summary>Convenience factory for the single-recipient case.</summary>
    /// <param name="to">Recipient address.</param>
    /// <param name="subject">Subject line.</param>
    /// <param name="textBody">Plain-text body.</param>
    /// <param name="htmlBody">Optional HTML body.</param>
    public static EmailMessage Create(string to, string subject, string? textBody = null, string? htmlBody = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        return new EmailMessage([new EmailAddress(to)], subject, textBody, htmlBody);
    }
}
