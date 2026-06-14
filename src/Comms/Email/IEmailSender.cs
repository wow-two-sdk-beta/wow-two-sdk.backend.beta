namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>
/// Transactional email seam — one implementation per provider (MailKit SMTP, SendGrid, SES, …).
/// Result-typed: failures come back as <see cref="EmailSendResult.FailureReason"/>, not exceptions
/// (cancellation excepted).
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends one message.</summary>
    /// <param name="message">The message; <c>From</c> falls back to <see cref="EmailOptions.DefaultFrom"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
