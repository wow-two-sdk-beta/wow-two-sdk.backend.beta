namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>
/// Defines behavior that integrates the SMS provider; one implementation per provider, failures returned via
/// <see cref="SmsSendResult.FailureReason"/> rather than thrown (cancellation excepted).
/// </summary>
public interface ISmsBroker
{
    /// <summary>Sends one message.</summary>
    /// <param name="message">The message; <c>From</c> falls back to <see cref="SmsOptions.DefaultFrom"/> and then the provider's own default.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}
