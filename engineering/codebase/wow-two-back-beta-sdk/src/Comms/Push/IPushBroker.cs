namespace WoW.Two.Sdk.Backend.Beta.Comms.Push;

/// <summary>
/// Defines behavior that integrates a push-notification provider; one implementation per provider, failures returned
/// via <see cref="PushSendResult.FailureReason"/> rather than thrown (cancellation excepted).
/// </summary>
public interface IPushBroker
{
    /// <summary>Sends one notification to one device.</summary>
    /// <param name="message">The notification and its device token.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default);
}
