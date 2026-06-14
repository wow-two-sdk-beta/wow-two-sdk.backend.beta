using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using WoW.Two.Sdk.Backend.Beta.comms.email;
using EmailAddress = WoW.Two.Sdk.Backend.Beta.comms.email.EmailAddress;

namespace WoW.Two.Sdk.Backend.Beta.comms.email_sendgrid;

/// <summary><see cref="IEmailSender"/> over the SendGrid v3 API.</summary>
public sealed class SendGridEmailSender : IEmailSender
{
    private readonly ISendGridClient _client;
    private readonly EmailOptions _emailOptions;

    /// <summary>Creates the sender over a SendGrid client.</summary>
    /// <param name="client">The SendGrid client (registered by <c>AddSendGridEmailSender</c>).</param>
    /// <param name="emailOptions">Cross-provider defaults (From / Reply-To).</param>
    public SendGridEmailSender(ISendGridClient client, IOptions<EmailOptions> emailOptions)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(emailOptions);
        _client = client;
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

        var mail = new SendGridMessage
        {
            From = ToSendGrid(from),
            Subject = message.Subject,
            PlainTextContent = message.TextBody,
            HtmlContent = message.HtmlBody,
        };

        foreach (var recipient in message.To)
        {
            mail.AddTo(ToSendGrid(recipient));
        }

        foreach (var recipient in message.Cc ?? [])
        {
            mail.AddCc(ToSendGrid(recipient));
        }

        foreach (var recipient in message.Bcc ?? [])
        {
            mail.AddBcc(ToSendGrid(recipient));
        }

        var replyTo = message.ReplyTo ?? _emailOptions.DefaultReplyTo;
        if (replyTo is not null)
        {
            mail.ReplyTo = ToSendGrid(replyTo);
        }

        foreach (var attachment in message.Attachments ?? [])
        {
            mail.AddAttachment(attachment.FileName, Convert.ToBase64String(attachment.Content.Span), attachment.ContentType);
        }

        try
        {
            var response = await _client.SendEmailAsync(mail, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var messageId = response.Headers is not null && response.Headers.TryGetValues("X-Message-Id", out var values)
                    ? values.FirstOrDefault()
                    : null;
                return new EmailSendResult(true, ProviderMessageId: messageId);
            }

            var body = await response.Body.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new EmailSendResult(false, FailureReason: $"sendgrid_{(int)response.StatusCode}: {body}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new EmailSendResult(false, FailureReason: exception.Message);
        }
    }

    private static global::SendGrid.Helpers.Mail.EmailAddress ToSendGrid(EmailAddress address) => new(address.Address, address.DisplayName);
}
