using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.MailKit;

/// <summary>
/// SMTP <see cref="IEmailSender"/> over MailKit — works against any SMTP relay
/// (provider SMTP endpoints, self-hosted, mailpit/mailhog in dev). Connects per send.
/// </summary>
public sealed class MailKitEmailSender : IEmailSender
{
    private readonly MailKitEmailOptions _smtpOptions;
    private readonly EmailOptions _emailOptions;

    /// <summary>Creates the sender from SMTP + email defaults.</summary>
    /// <param name="smtpOptions">SMTP connection settings.</param>
    /// <param name="emailOptions">Cross-provider defaults (From / Reply-To).</param>
    public MailKitEmailSender(IOptions<MailKitEmailOptions> smtpOptions, IOptions<EmailOptions> emailOptions)
    {
        ArgumentNullException.ThrowIfNull(smtpOptions);
        ArgumentNullException.ThrowIfNull(emailOptions);
        _smtpOptions = smtpOptions.Value;
        _emailOptions = emailOptions.Value;
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var from = message.From ?? _emailOptions.DefaultFrom;
        if (from is null)
        {
            return new EmailSendResult(false, FailureReason: "no_from_address");
        }

        if (string.IsNullOrWhiteSpace(_smtpOptions.Host))
        {
            return new EmailSendResult(false, FailureReason: "smtp_host_not_configured");
        }

        var mime = BuildMime(message, from);

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpOptions.Host, _smtpOptions.Port, _smtpOptions.SecureSocket, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(_smtpOptions.Username))
            {
                await client.AuthenticateAsync(_smtpOptions.Username, _smtpOptions.Password, cancellationToken).ConfigureAwait(false);
            }

            var serverResponse = await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
            return new EmailSendResult(true, ProviderMessageId: serverResponse);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new EmailSendResult(false, FailureReason: exception.Message);
        }
    }

    private MimeMessage BuildMime(EmailMessage message, EmailAddress from)
    {
        var mime = new MimeMessage();
        mime.From.Add(ToMailbox(from));

        var replyTo = message.ReplyTo ?? _emailOptions.DefaultReplyTo;
        if (replyTo is not null)
        {
            mime.ReplyTo.Add(ToMailbox(replyTo));
        }

        foreach (var recipient in message.To)
        {
            mime.To.Add(ToMailbox(recipient));
        }

        foreach (var recipient in message.Cc ?? [])
        {
            mime.Cc.Add(ToMailbox(recipient));
        }

        foreach (var recipient in message.Bcc ?? [])
        {
            mime.Bcc.Add(ToMailbox(recipient));
        }

        mime.Subject = message.Subject;

        var body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody };
        foreach (var attachment in message.Attachments ?? [])
        {
            body.Attachments.Add(attachment.FileName, attachment.Content.ToArray(), ContentType.Parse(attachment.ContentType));
        }

        mime.Body = body.ToMessageBody();
        return mime;
    }

    private static MailboxAddress ToMailbox(EmailAddress address) => new(address.DisplayName, address.Address);
}
