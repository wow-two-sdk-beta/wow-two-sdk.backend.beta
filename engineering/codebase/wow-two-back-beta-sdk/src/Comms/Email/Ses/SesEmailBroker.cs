using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Ses;

/// <summary>Integrates Amazon SES v2 as the email provider.</summary>
public sealed class SesEmailBroker : IEmailBroker
{
    private readonly IAmazonSimpleEmailServiceV2 _client;
    private readonly EmailOptions _emailOptions;

    /// <summary>Creates the sender over an SES v2 client.</summary>
    /// <param name="client">The SES client (registered by <c>AddSesEmailBroker</c>).</param>
    /// <param name="emailOptions">Cross-provider defaults (From / Reply-To).</param>
    public SesEmailBroker(IAmazonSimpleEmailServiceV2 client, EmailOptions emailOptions)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(emailOptions);
        _client = client;
        _emailOptions = emailOptions;
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var from = message.From ?? _emailOptions.DefaultFrom;
        if (from is null)
        {
            return new EmailSendResult { Success = false, FailureReason = "no_from_address" };
        }

        if (message.Attachments is { Count: > 0 })
        {
            return new EmailSendResult { Success = false, FailureReason = "attachments_not_supported_by_ses_simple_send" };
        }

        var body = new Body();
        if (message.TextBody is not null)
        {
            body.Text = new Content { Data = message.TextBody };
        }

        if (message.HtmlBody is not null)
        {
            body.Html = new Content { Data = message.HtmlBody };
        }

        var request = new SendEmailRequest
        {
            FromEmailAddress = Format(from),
            Destination = new Destination
            {
                ToAddresses = [.. message.To.Select(Format)],
                CcAddresses = [.. (message.Cc ?? []).Select(Format)],
                BccAddresses = [.. (message.Bcc ?? []).Select(Format)],
            },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = message.Subject },
                    Body = body,
                },
            },
        };

        var replyTo = message.ReplyTo ?? _emailOptions.DefaultReplyTo;
        if (replyTo is not null)
        {
            request.ReplyToAddresses = [Format(replyTo)];
        }

        try
        {
            var response = await _client.SendEmailAsync(request, cancellationToken).ConfigureAwait(false);
            return new EmailSendResult { Success = true, ProviderMessageId = response.MessageId };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new EmailSendResult { Success = false, FailureReason = exception.Message };
        }
    }

    private static string Format(EmailAddress address)
        => string.IsNullOrWhiteSpace(address.DisplayName) ? address.Address : $"{address.DisplayName} <{address.Address}>";
}
