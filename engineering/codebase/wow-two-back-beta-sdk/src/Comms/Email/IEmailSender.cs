namespace WoW.Two.Sdk.Backend.Beta.Comms.Email;

/// <summary>Sends transactional email; one implementation per provider, failures returned via <see cref="EmailSendResult.FailureReason"/> rather than thrown (cancellation excepted).</summary>
public interface IEmailSender
{
    /// <summary>Sends one message.</summary>
    /// <param name="message">The message; <c>From</c> falls back to <see cref="EmailOptions.DefaultFrom"/>.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
