namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;

/// <summary>
/// Defines behavior that integrates a WhatsApp Business provider; failures return through
/// <see cref="WhatsAppSendResult.FailureReason"/> rather than exceptions (cancellation excepted).
/// </summary>
public interface IWhatsAppBroker
{
    /// <summary>Sends one message: a template, or free text inside the recipient's 24-hour service window.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<WhatsAppSendResult> SendAsync(WhatsAppMessage message, CancellationToken cancellationToken = default);
}
