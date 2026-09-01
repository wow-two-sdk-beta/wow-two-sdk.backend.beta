namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Handles a notification — invoked once per registered handler.</summary>
public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    /// <summary>Handle the notification.</summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken);
}
